using MediatR;

namespace CampusEats.Api.Features.Order.Request
{ 
    public record CreateOrderRequest(
        Guid UserId, 
        List<Guid> MenuItemIds,
        List<Guid>? RedeemedMenuItemIds = null
    ) : IRequest<IResult>;
}