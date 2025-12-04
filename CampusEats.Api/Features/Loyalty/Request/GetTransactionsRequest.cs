using MediatR;

namespace CampusEats.Api.Features.Loyalty.Request;

public record GetTransactionsRequest(HttpContext HttpContext) : IRequest<IResult>;
