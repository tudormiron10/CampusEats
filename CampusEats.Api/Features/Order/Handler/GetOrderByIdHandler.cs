using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CampusEats.Api.Features.Orders
{
    public class GetOrderByIdHandler
    {
        private readonly CampusEatsDbContext _context;

        public GetOrderByIdHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(Guid orderId)
        {
            var response = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.OrderId == orderId)
                .Select(o => new DetailedOrderResponse(
                    o.OrderId,
                    o.UserId,
                    o.Status.ToString(),
                    o.TotalAmount,
                    o.OrderDate,
                    o.Items.Select(oi => new OrderItemResponse(
                        oi.MenuItemId,
                        oi.MenuItem != null ? oi.MenuItem.Name : string.Empty,
                        oi.UnitPrice,
                        oi.Quantity)).ToList()
                ))
                .FirstOrDefaultAsync();

            if (response == null)
            {
                return Results.NotFound("Order not found.");
            }

            return Results.Ok(response);
        }
    }
}