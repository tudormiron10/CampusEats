using MediatR;

namespace CampusEats.Api.Features.Order.Request
{
    public record GetOrderByIdRequest(System.Guid OrderId) : IRequest<IResult>;
}

