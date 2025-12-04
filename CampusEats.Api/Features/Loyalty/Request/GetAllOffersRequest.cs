using MediatR;

namespace CampusEats.Api.Features.Loyalty.Request;

public record GetAllOffersRequest(HttpContext HttpContext) : IRequest<IResult>;
