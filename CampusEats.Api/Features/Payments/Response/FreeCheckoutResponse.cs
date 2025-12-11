namespace CampusEats.Api.Features.Payments.Response;

/// <summary>
/// Response for free checkout (orders with only redeemed items, totalAmount = 0).
/// No Stripe payment required.
/// </summary>
public record FreeCheckoutResponse(
    Guid OrderId,
    string Message = "Order created successfully using loyalty points."
);

