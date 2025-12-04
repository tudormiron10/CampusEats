using MediatR;

namespace CampusEats.Api.Features.Loyalty.Request;

public record RedeemOfferRequest(Guid OfferId, HttpContext HttpContext) : IRequest<IResult>;
