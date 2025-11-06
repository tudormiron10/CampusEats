using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Orders
{
    public class GetAllOrdersHandler
    {
        private readonly CampusEatsDbContext _context;

        public GetAllOrdersHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle()
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
                .ToListAsync();

            return Results.Ok(orders);
        }
    }
}