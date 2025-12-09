using MediatR;

namespace CampusEats.Api.Features.Payments.Request;

/// <summary>
/// Initiates a checkout session. Creates PaymentIntent in Stripe
/// and stores cart data in PendingCheckout until payment succeeds.
/// </summary>
public record InitiateCheckoutRequest(
    List<CheckoutItemDto> Items,
    List<Guid>? RedeemedMenuItemIds = null,
    List<Guid>? PendingOfferIds = null
) : IRequest<IResult>;

public record CheckoutItemDto(
    Guid MenuItemId,
    int Quantity
);

