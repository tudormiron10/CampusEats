using MediatR;

namespace CampusEats.Api.Features.Loyalty.Request;

public record GetOffersRequest(HttpContext HttpContext) : IRequest<IResult>;
