using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.Loyalty;
using CampusEats.Api.Features.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Text.Json;
using Entities = CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Payments.Handler;

public class StripeWebhookHandler
{
    private readonly CampusEatsDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookHandler> _logger;
    private readonly IPublisher _publisher;

    public StripeWebhookHandler(
        CampusEatsDbContext context,
        IConfiguration configuration,
        ILogger<StripeWebhookHandler> logger,
        IPublisher publisher)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _publisher = publisher;
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

            // Handle different event types
            if (stripeEvent.Type == "payment_intent.succeeded")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                await HandlePaymentSucceeded(paymentIntent!, stripeEvent.Id);
            }
            else if (stripeEvent.Type == "payment_intent.payment_failed")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                await HandlePaymentFailed(paymentIntent!);
            }
            else if (stripeEvent.Type == "payment_intent.canceled")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                await HandlePaymentCanceled(paymentIntent!);
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
        // Find the pending checkout by PaymentIntent ID
        var pendingCheckout = await _context.PendingCheckouts
            .FirstOrDefaultAsync(pc => pc.StripePaymentIntentId == paymentIntent.Id && !pc.IsProcessed);

        if (pendingCheckout == null)
        {
            _logger.LogWarning("PendingCheckout not found for PaymentIntent {PaymentIntentId}", paymentIntent.Id);
            return;
        }

        // Parse items from JSON
        var itemsData = JsonSerializer.Deserialize<List<CheckoutItemData>>(pendingCheckout.ItemsJson);
        if (itemsData == null || !itemsData.Any())
        {
            _logger.LogError("Failed to parse items for PendingCheckout {CheckoutId}", pendingCheckout.PendingCheckoutId);
            return;
        }

        // Parse redeemed items if any
        List<Guid>? redeemedItemIds = null;
        if (!string.IsNullOrEmpty(pendingCheckout.RedeemedItemsJson))
        {
            redeemedItemIds = JsonSerializer.Deserialize<List<Guid>>(pendingCheckout.RedeemedItemsJson);
        }

        // Create the Order
        var order = new Entities.Order
        {
            OrderId = Guid.NewGuid(),
            UserId = pendingCheckout.UserId,
            Status = OrderStatus.Pending, // Staff will move to InPreparation
            TotalAmount = pendingCheckout.TotalAmount,
            OrderDate = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        // Add paid items
        foreach (var item in itemsData)
        {
            order.Items.Add(new OrderItem
            {
                OrderItemId = Guid.NewGuid(),
                OrderId = order.OrderId,
                MenuItemId = item.MenuItemId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        // Add redeemed items (free, UnitPrice = 0)
        if (redeemedItemIds != null)
        {
            foreach (var menuItemId in redeemedItemIds)
            {
                order.Items.Add(new OrderItem
                {
                    OrderItemId = Guid.NewGuid(),
                    OrderId = order.OrderId,
                    MenuItemId = menuItemId,
                    Quantity = 1,
                    UnitPrice = 0
                });
            }
        }

        _context.Orders.Add(order);

        // Create Payment record linked to the order
        var payment = new Entities.Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = pendingCheckout.TotalAmount,
            Status = PaymentStatus.Succeeded,
            StripePaymentIntentId = paymentIntent.Id,
            StripeEventId = eventId,
            CreatedAt = pendingCheckout.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);

        // Mark checkout as processed
        pendingCheckout.IsProcessed = true;

        // Load user with loyalty for point operations
        var user = await _context.Users
            .Include(u => u.Loyalty)
            .FirstOrDefaultAsync(u => u.UserId == pendingCheckout.UserId);

        // Redeem pending offers (deduct points) - only after successful payment
        if (!string.IsNullOrEmpty(pendingCheckout.PendingOfferIdsJson) && user?.Loyalty != null)
        {
            var pendingOfferIds = JsonSerializer.Deserialize<List<Guid>>(pendingCheckout.PendingOfferIdsJson);
            if (pendingOfferIds != null && pendingOfferIds.Any())
            {
                var offers = await _context.Offers
                    .Where(o => pendingOfferIds.Contains(o.OfferId))
                    .ToListAsync();

                foreach (var offer in offers)
                {
                    // Deduct points for the offer
                    user.Loyalty.CurrentPoints -= offer.PointCost;
                    
                    // Create redemption transaction
                    var redeemTransaction = new LoyaltyTransaction
                    {
                        TransactionId = Guid.NewGuid(),
                        Date = DateTime.UtcNow,
                        Description = $"Redeemed: {offer.Title}",
                        Type = "Redeemed",
                        Points = -offer.PointCost,
                        OrderId = order.OrderId,
                        LoyaltyId = user.Loyalty.LoyaltyId
                    };

                    _context.LoyaltyTransactions.Add(redeemTransaction);

                    _logger.LogInformation(
                        "Redeemed offer {OfferId} ({OfferTitle}) for user {UserId}, deducted {Points} points",
                        offer.OfferId,
                        offer.Title,
                        pendingCheckout.UserId,
                        offer.PointCost
                    );
                }
            }
        }

        // Award loyalty points for the paid amount
        if (user?.Loyalty != null && pendingCheckout.TotalAmount > 0)
        {
            var tier = LoyaltyHelpers.CalculateTier(user.Loyalty.LifetimePoints);
            var earnRate = LoyaltyHelpers.GetEarnRate(tier);
            var pointsEarned = (int)(pendingCheckout.TotalAmount * earnRate);

            user.Loyalty.CurrentPoints += pointsEarned;
            user.Loyalty.LifetimePoints += pointsEarned;

            var orderIdShort = order.OrderId.ToString().Substring(0, 8);
            var transaction = new LoyaltyTransaction
            {
                TransactionId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                Description = "Order #" + orderIdShort,
                Type = "Earned",
                Points = pointsEarned,
                OrderId = order.OrderId,
                LoyaltyId = user.Loyalty.LoyaltyId
            };

            _context.LoyaltyTransactions.Add(transaction);

            _logger.LogInformation(
                "Awarded {Points} loyalty points to user {UserId} for order {OrderId}",
                pointsEarned,
                pendingCheckout.UserId,
                order.OrderId
            );
        }

        await _context.SaveChangesAsync();

        // Publish notification to notify kitchen staff of new order via SignalR
        try
        {
            var customerName = user?.Name ?? "Customer";
            await _publisher.Publish(new OrderCreatedNotification(
                order.OrderId,
                order.UserId,
                customerName,
                order.TotalAmount,
                order.OrderDate,
                order.Items.Select(oi => new OrderItemNotification(
                    oi.MenuItemId,
                    itemsData.FirstOrDefault(i => i.MenuItemId == oi.MenuItemId)?.Name ?? string.Empty,
                    oi.Quantity,
                    oi.UnitPrice
                )).ToList()
            ));

            _logger.LogInformation(
                "Sent new order notification to kitchen for order {OrderId}",
                order.OrderId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification for order {OrderId}", order.OrderId);
            // Don't fail the whole operation if SignalR notification fails
        }

        _logger.LogInformation(
            "Payment succeeded! Created Order {OrderId} from PendingCheckout {CheckoutId}",
            order.OrderId,
            pendingCheckout.PendingCheckoutId
        );
    }

    private async Task HandlePaymentFailed(PaymentIntent paymentIntent)
    {
        var pendingCheckout = await _context.PendingCheckouts
            .FirstOrDefaultAsync(pc => pc.StripePaymentIntentId == paymentIntent.Id && !pc.IsProcessed);

        if (pendingCheckout == null)
        {
            _logger.LogWarning("PendingCheckout not found for failed PaymentIntent {PaymentIntentId}", paymentIntent.Id);
            return;
        }

        // Mark as processed (failed) - don't create order
        pendingCheckout.IsProcessed = true;
        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "Payment failed for PendingCheckout {CheckoutId}. No order created.",
            pendingCheckout.PendingCheckoutId
        );
    }

    private async Task HandlePaymentCanceled(PaymentIntent paymentIntent)
    {
        var pendingCheckout = await _context.PendingCheckouts
            .FirstOrDefaultAsync(pc => pc.StripePaymentIntentId == paymentIntent.Id && !pc.IsProcessed);

        if (pendingCheckout == null)
        {
            _logger.LogWarning("PendingCheckout not found for cancelled PaymentIntent {PaymentIntentId}", paymentIntent.Id);
            return;
        }

        // Mark as processed (cancelled) - don't create order
        pendingCheckout.IsProcessed = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Payment cancelled for PendingCheckout {CheckoutId}. No order created.",
            pendingCheckout.PendingCheckoutId
        );
    }

    // Helper class for JSON deserialization
    private sealed class CheckoutItemData
    {
        public Guid MenuItemId { get; init; }
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public string Name { get; init; } = null!;
    }
}
