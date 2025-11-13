using CampusEats.Api.Infrastructure.Persistence.Entities;
using MediatR;

namespace CampusEats.Api.Features.Orders.Request
{
    public record UpdateOrderRequest(OrderStatus NewStatus) : IRequest<IResult>;
}