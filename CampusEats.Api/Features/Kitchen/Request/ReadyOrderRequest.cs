using MediatR;

namespace CampusEats.Api.Features.Kitchen.Request
{
    public record ReadyOrderRequest(System.Guid OrderId) : IRequest<IResult>;
}

