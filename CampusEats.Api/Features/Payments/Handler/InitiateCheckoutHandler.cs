using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Features.Payments.Response;
using CampusEats.Api.Features.Loyalty;
using CampusEats.Api.Features.Notifications;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Text.Json;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Features.Payments.Handler;

public class InitiateCheckoutHandler : IRequestHandler<InitiateCheckoutRequest, IResult>
{
    private readonly CampusEatsDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InitiateCheckoutHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPublisher _publisher;

    public InitiateCheckoutHandler(
        CampusEatsDbContext context,
        IConfiguration configuration,
        ILogger<InitiateCheckoutHandler> logger,
        IHttpContextAccessor httpContextAccessor,
        IPublisher publisher)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _publisher = publisher;
    }

    public async Task<IResult> Handle(InitiateCheckoutRequest request, CancellationToken cancellationToken)
    {
        // Get user ID from JWT using the extension method
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return Results.Unauthorized();
        }
        
        var userIdNullable = httpContext.GetUserId();
        if (userIdNullable == null)
        {
            return Results.Unauthorized();
        }
        
        var userId = userIdNullable.Value;

        // Validate request - must have either paid items or redeemed items
        var hasPaidItems = request.Items != null && request.Items.Count > 0;
        var hasRedeemedItems = request.RedeemedMenuItemIds != null && request.RedeemedMenuItemIds.Count > 0;
        var hasOffers = request.PendingOfferIds != null && request.PendingOfferIds.Count > 0;

        if (!hasPaidItems && !hasRedeemedItems)
        {
            return Results.BadRequest("Cart cannot be empty.");
        }

        // Calculate total from paid items
        decimal totalAmount = 0;
        var itemsData = new List<CheckoutItemData>();

        if (hasPaidItems)
        {
            // Fetch menu items to get prices
            var menuItemIds = request.Items!.Select(i => i.MenuItemId).ToList();
            var menuItems = await _context.MenuItems
                .Where(m => menuItemIds.Contains(m.MenuItemId))
                .ToDictionaryAsync(m => m.MenuItemId, cancellationToken);

            // Validate all items exist
            var missingItems = menuItemIds.Except(menuItems.Keys).ToList();
            if (missingItems.Count > 0)
            {
                return Results.BadRequest($"Menu items not found: {string.Join(", ", missingItems)}");
            }

            // Build items data with prices for storage
            itemsData = request.Items!.Select(i => new CheckoutItemData
            {
                MenuItemId = i.MenuItemId,
                Quantity = i.Quantity,
                UnitPrice = menuItems[i.MenuItemId].Price,
                Name = menuItems[i.MenuItemId].Name
            }).ToList();

            // Calculate total (paid items only)
            totalAmount = itemsData.Sum(i => i.UnitPrice * i.Quantity);
        }

        // FREE CHECKOUT FLOW - Only redeemed items, no payment needed
        if (totalAmount == 0 && hasRedeemedItems && hasOffers)
        {
            return await HandleFreeCheckout(userId, request.RedeemedMenuItemIds!, request.PendingOfferIds!, cancellationToken);
        }

        // PAID CHECKOUT FLOW - Stripe payment required
        if (totalAmount <= 0)
        {
            return Results.BadRequest("Total amount must be greater than zero for paid checkout.");
        }

        // Configure Stripe
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        // Create Stripe PaymentIntent
        var paymentIntentService = new PaymentIntentService();
        var paymentIntentOptions = new PaymentIntentCreateOptions
        {
            Amount = (long)(totalAmount * 100), // Stripe uses smallest currency unit (bani for RON)
            Currency = "ron",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never"
            },
            Metadata = new Dictionary<string, string>
            {
                { "user_id", userId.ToString() }
            }
        };

        PaymentIntent paymentIntent;
        try
        {
            paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions, cancellationToken: cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error while creating PaymentIntent for user {UserId}", userId);
            return Results.Problem(
                detail: "Failed to initiate payment with payment provider. Please try again.",
                statusCode: 500
            );
        }

        // Create PendingCheckout record
        var pendingCheckout = new PendingCheckout
        {
            PendingCheckoutId = Guid.NewGuid(),
            UserId = userId,
            ItemsJson = JsonSerializer.Serialize(itemsData),
            RedeemedItemsJson = request.RedeemedMenuItemIds != null 
                ? JsonSerializer.Serialize(request.RedeemedMenuItemIds) 
                : null,
            PendingOfferIdsJson = request.PendingOfferIds != null
                ? JsonSerializer.Serialize(request.PendingOfferIds)
                : null,
            TotalAmount = totalAmount,
            StripePaymentIntentId = paymentIntent.Id,
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false
        };

        _context.PendingCheckouts.Add(pendingCheckout);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Checkout initiated for user {UserId}. PendingCheckoutId: {CheckoutId}, Amount: {Amount} RON",
            userId,
            pendingCheckout.PendingCheckoutId,
            totalAmount
        );

        var response = new CheckoutSessionResponse(
            pendingCheckout.PendingCheckoutId,
            paymentIntent.ClientSecret,
            totalAmount,
            _configuration["Stripe:PublishableKey"] ?? ""
        );

        return Results.Ok(response);
    }

    /// <summary>
    /// Handles checkout for orders with only redeemed items (totalAmount = 0).
    /// Creates order directly without Stripe payment, deducts loyalty points.
    /// </summary>
    private async Task<IResult> HandleFreeCheckout(
        Guid userId,
        List<Guid> redeemedMenuItemIds,
        List<Guid> pendingOfferIds,
        CancellationToken cancellationToken)
    {
        // Load user with loyalty
        var user = await _context.Users
            .Include(u => u.Loyalty)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            return ApiErrors.UserNotFound();
        }

        if (user.Loyalty == null)
        {
            return Results.BadRequest("User does not have a loyalty account.");
        }

        // Load offers and validate
        var offers = await _context.Offers
            .Where(o => pendingOfferIds.Contains(o.OfferId) && o.IsActive)
            .ToListAsync(cancellationToken);

        if (offers.Count != pendingOfferIds.Count)
        {
            var foundIds = offers.Select(o => o.OfferId).ToList();
            var missingIds = pendingOfferIds.Except(foundIds).ToList();
            return Results.BadRequest($"Some offers not found or inactive: {string.Join(", ", missingIds)}");
        }

        // Calculate total points needed
        var totalPointsNeeded = offers.Sum(o => o.PointCost);

        // Validate user has enough points
        if (user.Loyalty.CurrentPoints < totalPointsNeeded)
        {
            return Results.BadRequest(
                $"Insufficient loyalty points. Required: {totalPointsNeeded}, Available: {user.Loyalty.CurrentPoints}");
        }

        // Validate tier requirements for each offer
        var userTier = LoyaltyHelpers.CalculateTier(user.Loyalty.LifetimePoints);
        var invalidTierOffer = offers.FirstOrDefault(o => !LoyaltyHelpers.MeetsTierRequirement(userTier, o.MinimumTier));
        if (invalidTierOffer != null)
        {
            return Results.BadRequest(
                $"You don't have the required tier for offer '{invalidTierOffer.Title}'. Required: {invalidTierOffer.MinimumTier}, Your tier: {userTier}");
        }

        // Validate redeemed menu items exist
        var redeemedMenuItems = await _context.MenuItems
            .Where(m => redeemedMenuItemIds.Contains(m.MenuItemId))
            .ToDictionaryAsync(m => m.MenuItemId, cancellationToken);

        var missingMenuItems = redeemedMenuItemIds.Except(redeemedMenuItems.Keys).ToList();
        if (missingMenuItems.Count > 0)
        {
            return Results.BadRequest($"Menu items not found: {string.Join(", ", missingMenuItems)}");
        }

        // Create Order
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = userId,
            Status = OrderStatus.Pending,
            TotalAmount = 0, // Free order
            OrderDate = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        // Add redeemed items (free, UnitPrice = 0)
        foreach (var menuItemId in redeemedMenuItemIds)
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

        _context.Orders.Add(order);

        // Deduct points and create loyalty transactions for each offer
        foreach (var offer in offers)
        {
            user.Loyalty.CurrentPoints -= offer.PointCost;

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
                userId,
                offer.PointCost
            );
        }

        // Save everything in one transaction
        await _context.SaveChangesAsync(cancellationToken);

        // Publish notification to notify kitchen staff of new order via SignalR
        try
        {
            await _publisher.Publish(new OrderCreatedNotification(
                order.OrderId,
                order.UserId,
                user.Name,
                order.TotalAmount,
                order.OrderDate,
                order.Items.Select(oi => new OrderItemNotification(
                    oi.MenuItemId,
                    redeemedMenuItems.GetValueOrDefault(oi.MenuItemId)?.Name ?? string.Empty,
                    oi.Quantity,
                    oi.UnitPrice
                )).ToList()
            ), cancellationToken);

            _logger.LogInformation(
                "Sent new order notification to kitchen for free order {OrderId}",
                order.OrderId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification for free order {OrderId}", order.OrderId);
            // Don't fail the whole operation if SignalR notification fails
        }

        _logger.LogInformation(
            "Free checkout completed! Created Order {OrderId} for user {UserId} using {TotalPoints} loyalty points",
            order.OrderId,
            userId,
            totalPointsNeeded
        );

        return Results.Created($"/orders/{order.OrderId}", new FreeCheckoutResponse(order.OrderId));
    }
}
