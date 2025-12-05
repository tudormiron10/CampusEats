using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.Loyalty;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace CampusEats.Api.Features.Payments.Handler;

public class StripeWebhookHandler
{
    private readonly CampusEatsDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookHandler> _logger;

    public StripeWebhookHandler(
        CampusEatsDbContext context,
        IConfiguration configuration,
        ILogger<StripeWebhookHandler> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IResult> HandleWebhook(HttpRequest request)
    {
        var json = await new StreamReader(request.Body).ReadToEndAsync();
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        try
        {
            // Verify webhook signature (CRITICAL FOR SECURITY)
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                request.Headers["Stripe-Signature"],
                webhookSecret
            );

            _logger.LogInformation("Stripe webhook received: {EventType} (ID: {EventId})", 
                stripeEvent.Type, stripeEvent.Id);

            // Check idempotency - have we already processed this event?
            var alreadyProcessed = await _context.Payments
                .AnyAsync(p => p.StripeEventId == stripeEvent.Id);

            if (alreadyProcessed)
            {
                _logger.LogWarning("Webhook event {EventId} already processed. Skipping.", stripeEvent.Id);
                return Results.Ok(new { received = true, message = "Event already processed" });
            }

            // Handle different event types
            if (stripeEvent.Type == "payment_intent.succeeded")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                await HandlePaymentSucceeded(paymentIntent!, stripeEvent.Id);
            }
            else if (stripeEvent.Type == "payment_intent.payment_failed")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                await HandlePaymentFailed(paymentIntent!, stripeEvent.Id);
            }
            else if (stripeEvent.Type == "payment_intent.canceled")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                await HandlePaymentCanceled(paymentIntent!, stripeEvent.Id);
            }
            else
            {
                _logger.LogInformation("Unhandled event type: {EventType}", stripeEvent.Type);
            }

            return Results.Ok(new { received = true });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature verification failed");
            return Results.BadRequest(new { error = "Invalid signature" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return Results.Problem("Error processing webhook");
        }
    }

    private async Task HandlePaymentSucceeded(PaymentIntent paymentIntent, string eventId)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

        if (payment == null)
        {
            _logger.LogWarning("Payment not found for PaymentIntent {PaymentIntentId}", paymentIntent.Id);
            return;
        }

        // Update payment status
        payment.Status = PaymentStatus.Succeeded;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.StripeEventId = eventId; // For idempotency

        // Update order status
        if (payment.Order != null)
        {
            payment.Order.Status = OrderStatus.InPreparation;
        }

        if (payment.Order != null)
        {
            var user = await _context.Users
                .Include(u => u.Loyalty)
                .FirstOrDefaultAsync(u => u.UserId == payment.Order.UserId);

            if (user?.Loyalty != null)
            {
                // Calculate points based on current tier
                var tier = LoyaltyHelpers.CalculateTier(user.Loyalty.LifetimePoints);
                var earnRate = LoyaltyHelpers.GetEarnRate(tier);
                var pointsEarned = (int)(payment.Order.TotalAmount * earnRate);

                // Add points
                user.Loyalty.CurrentPoints += pointsEarned;
                user.Loyalty.LifetimePoints += pointsEarned;

                // Create transaction record
                var transaction = new LoyaltyTransaction
                {
                    TransactionId = Guid.NewGuid(),
                    Date = DateTime.UtcNow,
                    Description = $"Order #{payment.Order.OrderId.ToString()[..8]}",
                    Type = "Earned",
                    Points = pointsEarned,
                    OrderId = payment.Order.OrderId,
                    LoyaltyId = user.Loyalty.LoyaltyId
                };

                _context.LoyaltyTransactions.Add(transaction);

                _logger.LogInformation(
                    "Awarded {Points} loyalty points to user {UserId} for order {OrderId}",
                    pointsEarned,
                    payment.Order.UserId,
                    payment.Order.OrderId
                );
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Payment {PaymentId} succeeded for order {OrderId}. Order moved to InPreparation.",
            payment.PaymentId,
            payment.OrderId
        );
    }

    private async Task HandlePaymentFailed(PaymentIntent paymentIntent, string eventId)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

        if (payment == null)
        {
            _logger.LogWarning("Payment not found for failed PaymentIntent {PaymentIntentId}", paymentIntent.Id);
            return;
        }

        payment.Status = PaymentStatus.Failed;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.StripeEventId = eventId;

        // Cancel the order
        if (payment.Order != null)
        {
            payment.Order.Status = OrderStatus.Cancelled;
        }

        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "Payment {PaymentId} failed for order {OrderId}. Order cancelled.",
            payment.PaymentId,
            payment.OrderId
        );
    }

    private async Task HandlePaymentCanceled(PaymentIntent paymentIntent, string eventId)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

        if (payment == null)
        {
            _logger.LogWarning("Payment not found for cancelled PaymentIntent {PaymentIntentId}", paymentIntent.Id);
            return;
        }

        payment.Status = PaymentStatus.Cancelled;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.StripeEventId = eventId;

        if (payment.Order != null)
        {
            payment.Order.Status = OrderStatus.Cancelled;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Payment {PaymentId} cancelled for order {OrderId}",
            payment.PaymentId,
            payment.OrderId
        );
    }
}
