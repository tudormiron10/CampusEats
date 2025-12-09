namespace CampusEats.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Stores cart data temporarily until payment succeeds.
/// When payment succeeds via webhook, this data is used to create the actual Order.
/// </summary>
public class PendingCheckout
{
    public Guid PendingCheckoutId { get; set; }
    
    /// <summary>
    /// The user who initiated the checkout.
    /// </summary>
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    /// <summary>
    /// JSON serialized list of items: [{MenuItemId, Quantity, UnitPrice, Name}]
    /// </summary>
    public string ItemsJson { get; set; } = null!;
    
    /// <summary>
    /// JSON serialized list of redeemed menu item IDs (loyalty rewards).
    /// </summary>
    public string? RedeemedItemsJson { get; set; }
    
    /// <summary>
    /// JSON serialized list of pending offer IDs to be redeemed after successful payment.
    /// </summary>
    public string? PendingOfferIdsJson { get; set; }
    
    /// <summary>
    /// Total amount to be charged (excludes redeemed items).
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Stripe PaymentIntent ID for correlation.
    /// </summary>
    public string StripePaymentIntentId { get; set; } = null!;
    
    /// <summary>
    /// When the checkout was initiated.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Whether this checkout has been processed (converted to order or expired).
    /// </summary>
    public bool IsProcessed { get; set; }
}

