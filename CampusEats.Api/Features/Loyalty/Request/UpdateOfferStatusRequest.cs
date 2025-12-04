using MediatR;

namespace CampusEats.Api.Features.Loyalty.Request;

public record UpdateOfferStatusRequest(
    Guid OfferId,
    bool IsActive,
    HttpContext HttpContext
) : IRequest<IResult>;
