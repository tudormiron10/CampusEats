using MediatR;

namespace CampusEats.Api.Features.Kitchen.Request
{
    public record CompleteOrderRequest(System.Guid OrderId) : IRequest<IResult>;
}

