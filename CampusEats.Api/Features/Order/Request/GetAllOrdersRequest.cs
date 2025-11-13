using MediatR;

namespace CampusEats.Api.Features.Order.Request
{
    public record GetAllOrdersRequest() : IRequest<IResult>;
}

