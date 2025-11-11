using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Orders;
using System.Linq;

namespace CampusEats.Api.Features.Kitchen
{
    public class CompleteOrderHandler
    {
        private readonly CampusEatsDbContext _context;
        public CompleteOrderHandler(CampusEatsDbContext context) { _context = context; }

        public async Task<IResult> Handle(Guid orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return Results.NotFound("Order not found.");

            if (order.Status != OrderStatus.Ready)
            {
                return Results.BadRequest("Only orders marked 'Ready' can be completed.");
            }

            order.Status = OrderStatus.Completed;
            await _context.SaveChangesAsync();

            var response = new DetailedOrderResponse(
                order.OrderId,
                order.UserId,
                order.Status.ToString(),
                order.TotalAmount,
                order.OrderDate,
                order.Items.Select(oi => new OrderItemResponse(
                    oi.MenuItemId,
                    oi.MenuItem != null ? oi.MenuItem.Name : string.Empty,
                    oi.UnitPrice,
                    oi.Quantity)).ToList()
            );

            return Results.Ok(response);
        }
    }
}