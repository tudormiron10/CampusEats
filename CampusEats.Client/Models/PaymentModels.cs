namespace CampusEats.Client.Models;

// Request to initiate checkout
public record InitiateCheckoutRequest(
    List<CheckoutItemDto> Items,
    List<Guid>? RedeemedMenuItemIds = null,
    List<Guid>? PendingOfferIds = null
);

public record CheckoutItemDto(
    Guid MenuItemId,
    int Quantity
);

// Response from checkout initiation
public record CheckoutSessionResponse(
    Guid PendingCheckoutId,
    string ClientSecret,
    decimal TotalAmount,
    string PublishableKey
);

// Free checkout response (order created without payment)
public record FreeCheckoutResponse(
    Guid OrderId,
    string Message
);

// Wrapper result for InitiateCheckout to support both paid and free flows
public class InitiateCheckoutResult
{
    public CheckoutSessionResponse? Session { get; set; }
    public FreeCheckoutResponse? Free { get; set; }
}

// Payment history item
public record PaymentHistoryItem(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string? ClientSecret
);
