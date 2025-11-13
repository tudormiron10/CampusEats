using MediatR;

namespace CampusEats.Api.Features.Kitchen.Request
{
    public record PrepareOrderRequest(System.Guid OrderId) : IRequest<IResult>;
}

