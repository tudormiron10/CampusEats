using MediatR;

namespace CampusEats.Api.Features.Loyalty.Request;

public record UpdateOfferRequest(
    Guid OfferId,
    string Title,
    string? Description,
    string? ImageUrl,
    int PointCost,
    string? MinimumTier,
    List<CreateOfferItemRequest> Items,
    HttpContext HttpContext
) : IRequest<IResult>;
