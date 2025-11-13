using MediatR;

namespace CampusEats.Api.Features.Kitchen.Request
{
    public record GetKitchenOrdersRequest() : IRequest<IResult>;
}

