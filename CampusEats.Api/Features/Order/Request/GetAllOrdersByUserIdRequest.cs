using MediatR;

namespace CampusEats.Api.Features.Orders.Requests
{
    public record GetAllOrdersByUserIdRequest(Guid UserId) : IRequest<IResult>;
}

