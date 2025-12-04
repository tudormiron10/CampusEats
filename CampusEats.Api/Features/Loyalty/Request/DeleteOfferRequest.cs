using MediatR;

namespace CampusEats.Api.Features.Loyalty.Request;

public record DeleteOfferRequest(Guid OfferId, HttpContext HttpContext) : IRequest<IResult>;
