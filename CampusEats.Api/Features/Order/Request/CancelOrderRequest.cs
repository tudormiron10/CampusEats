using MediatR;

namespace CampusEats.Api.Features.Order.Request
{
    public record CancelOrderRequest(System.Guid OrderId) : IRequest<IResult>;
}

