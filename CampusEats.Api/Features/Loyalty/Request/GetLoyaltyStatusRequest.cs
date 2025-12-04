using MediatR;
namespace CampusEats.Api.Features.Loyalty.Request;
public record GetLoyaltyStatusRequest(HttpContext HttpContext) : IRequest<IResult>;

