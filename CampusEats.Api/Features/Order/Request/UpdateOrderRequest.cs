using CampusEats.Api.Infrastructure.Persistence.Entities;
using MediatR;

namespace CampusEats.Api.Features.Order.Request
{
    public record UpdateOrderRequest(OrderStatus NewStatus) : IRequest<IResult>;
}