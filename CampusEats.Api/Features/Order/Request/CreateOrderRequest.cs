using MediatR;

namespace CampusEats.Api.Features.Order.Request
{ 
    public record CreateOrderRequest(Guid UserId, List<Guid> MenuItemIds) : IRequest<IResult>;
}