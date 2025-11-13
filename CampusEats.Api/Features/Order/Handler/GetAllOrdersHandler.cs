using CampusEats.Api.Features.Order.Request;
using CampusEats.Api.Features.Orders.Response;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Infrastructure.Persistence;

namespace CampusEats.Api.Features.Order.Handler
{
    
    public class GetAllOrdersHandler : IRequestHandler<GetAllOrdersRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public GetAllOrdersHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(GetAllOrdersRequest request, CancellationToken cancellationToken)
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Select(o => new SimpleOrderResponse(
                    o.OrderId,
                    o.UserId,
                    o.Status.ToString(),
                    o.TotalAmount,
                    o.OrderDate
                ))
                .ToListAsync(cancellationToken);

            return Results.Ok(orders);
        }
    }
}