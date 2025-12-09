using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Features.Payments.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Text.Json;

namespace CampusEats.Api.Features.Payments.Handler;

public class InitiateCheckoutHandler : IRequestHandler<InitiateCheckoutRequest, IResult>
{
    private readonly CampusEatsDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InitiateCheckoutHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InitiateCheckoutHandler(
        CampusEatsDbContext context,
        IConfiguration configuration,
        ILogger<InitiateCheckoutHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
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

        // Validate request
        if (request.Items == null || !request.Items.Any())
        {
            return Results.BadRequest("Cart cannot be empty.");
        }

        // Fetch menu items to get prices
        var menuItemIds = request.Items.Select(i => i.MenuItemId).ToList();
        var menuItems = await _context.MenuItems
            .Where(m => menuItemIds.Contains(m.MenuItemId))
            .ToDictionaryAsync(m => m.MenuItemId, cancellationToken);

        // Validate all items exist
        var missingItems = menuItemIds.Except(menuItems.Keys).ToList();
        if (missingItems.Any())
        {
            return Results.BadRequest($"Menu items not found: {string.Join(", ", missingItems)}");
        }

        // Build items data with prices for storage
        var itemsData = request.Items.Select(i => new
        {
            MenuItemId = i.MenuItemId,
            Quantity = i.Quantity,
            UnitPrice = menuItems[i.MenuItemId].Price,
            Name = menuItems[i.MenuItemId].Name
        }).ToList();

        // Calculate total (paid items only)
        decimal totalAmount = itemsData.Sum(i => i.UnitPrice * i.Quantity);

        if (totalAmount <= 0)
        {
            return Results.BadRequest("Total amount must be greater than zero.");
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
}

