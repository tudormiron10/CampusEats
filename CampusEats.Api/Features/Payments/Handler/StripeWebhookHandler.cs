﻿using CampusEats.Api.Infrastructure.Persistence;
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
        if (itemsData == null || itemsData.Count == 0)
        {
            _logger.LogError("Failed to parse items for PendingCheckout {CheckoutId}", pendingCheckout.PendingCheckoutId);
            return;
        }

        // Parse redeemed items if any
        var redeemedItemIds = ParseRedeemedItems(pendingCheckout.RedeemedItemsJson);

        // Create the Order with all items
        var order = CreateOrderFromCheckout(pendingCheckout, itemsData, redeemedItemIds);
        _context.Orders.Add(order);

        // Create Payment record linked to the order
        var payment = CreatePaymentRecord(order.OrderId, pendingCheckout, paymentIntent.Id, eventId);
        _context.Payments.Add(payment);

        // Mark checkout as processed
        pendingCheckout.IsProcessed = true;

        // Load user with loyalty for point operations
        var user = await _context.Users
            .Include(u => u.Loyalty)
            .FirstOrDefaultAsync(u => u.UserId == pendingCheckout.UserId);

        // Process loyalty operations
        await ProcessOfferRedemptions(pendingCheckout, user, order.OrderId);
        ProcessLoyaltyPointsEarned(user, pendingCheckout, order.OrderId);

        await _context.SaveChangesAsync();

        // Send notification to kitchen
        await SendKitchenNotification(order, user, itemsData);

        _logger.LogInformation(
            "Payment succeeded! Created Order {OrderId} from PendingCheckout {CheckoutId}",
            order.OrderId,
            pendingCheckout.PendingCheckoutId
        );
    }

    private static List<Guid>? ParseRedeemedItems(string? redeemedItemsJson)
    {
        if (string.IsNullOrEmpty(redeemedItemsJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<Guid>>(redeemedItemsJson);
    }

    private static Entities.Order CreateOrderFromCheckout(
        PendingCheckout pendingCheckout,
        List<CheckoutItemData> itemsData,
        List<Guid>? redeemedItemIds)
    {
        var order = new Entities.Order
        {
            OrderId = Guid.NewGuid(),
            UserId = pendingCheckout.UserId,
            Status = OrderStatus.Pending,
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

        return order;
    }

    private static Entities.Payment CreatePaymentRecord(
        Guid orderId,
        PendingCheckout pendingCheckout,
        string paymentIntentId,
        string eventId)
    {
        return new Entities.Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            Amount = pendingCheckout.TotalAmount,
            Status = PaymentStatus.Succeeded,
            StripePaymentIntentId = paymentIntentId,
            StripeEventId = eventId,
            CreatedAt = pendingCheckout.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task ProcessOfferRedemptions(PendingCheckout pendingCheckout, Entities.User? user, Guid orderId)
    {
        if (string.IsNullOrEmpty(pendingCheckout.PendingOfferIdsJson) || user?.Loyalty == null)
        {
            return;
        }

        var pendingOfferIds = JsonSerializer.Deserialize<List<Guid>>(pendingCheckout.PendingOfferIdsJson);
        if (pendingOfferIds == null || pendingOfferIds.Count == 0)
        {
            return;
        }

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
                OrderId = orderId,
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

    private void ProcessLoyaltyPointsEarned(Entities.User? user, PendingCheckout pendingCheckout, Guid orderId)
    {
        if (user?.Loyalty == null || pendingCheckout.TotalAmount <= 0)
        {
            return;
        }

        var tier = LoyaltyHelpers.CalculateTier(user.Loyalty.LifetimePoints);
        var earnRate = LoyaltyHelpers.GetEarnRate(tier);
        var pointsEarned = (int)(pendingCheckout.TotalAmount * earnRate);

        user.Loyalty.CurrentPoints += pointsEarned;
        user.Loyalty.LifetimePoints += pointsEarned;

        var orderIdShort = orderId.ToString().Substring(0, 8);
        var transaction = new LoyaltyTransaction
        {
            TransactionId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            Description = "Order #" + orderIdShort,
            Type = "Earned",
            Points = pointsEarned,
            OrderId = orderId,
            LoyaltyId = user.Loyalty.LoyaltyId
        };

        _context.LoyaltyTransactions.Add(transaction);

        _logger.LogInformation(
            "Awarded {Points} loyalty points to user {UserId} for order {OrderId}",
            pointsEarned,
            pendingCheckout.UserId,
            orderId
        );
    }

    private async Task SendKitchenNotification(Entities.Order order, Entities.User? user, List<CheckoutItemData> itemsData)
    {
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
                    oi.MenuItemId.HasValue ? (itemsData.FirstOrDefault(i => i.MenuItemId == oi.MenuItemId.Value)?.Name ?? string.Empty) : string.Empty,
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
}
