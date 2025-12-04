using MediatR;

namespace CampusEats.Api.Features.Loyalty.Request;

public record CreateOfferRequest(
    string Title,
    string? Description,
    string? ImageUrl,
    int PointCost,
    string? MinimumTier,
    List<CreateOfferItemRequest> Items,
    HttpContext HttpContext
) : IRequest<IResult>;

public record CreateOfferItemRequest(
    Guid MenuItemId,
    int Quantity
);
