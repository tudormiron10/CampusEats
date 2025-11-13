using MediatR;

namespace CampusEats.Api.Features.Orders.Request
{ 
    public record CreateOrderRequest(Guid UserId, List<Guid> MenuItemIds) : IRequest<IResult>;
}