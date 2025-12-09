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

// Payment history item
public record PaymentHistoryItem(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string? ClientSecret
);

