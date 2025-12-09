namespace CampusEats.Api.Features.Payments.Response;

public record CheckoutSessionResponse(
    Guid PendingCheckoutId,
    string ClientSecret,
    decimal TotalAmount,
    string PublishableKey
);

