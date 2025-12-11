namespace CampusEats.Api.Features.Payments;

/// <summary>
/// Helper class for JSON serialization/deserialization of checkout items.
/// Used by InitiateCheckoutHandler and StripeWebhookHandler.
/// </summary>
public sealed class CheckoutItemData
{
    public Guid MenuItemId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Name { get; init; } = null!;
}

