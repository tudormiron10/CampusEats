using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CampusEats.Api.Features.Orders
{
    public class CancelOrderHandler
    {
        private readonly CampusEatsDbContext _context;

        public CancelOrderHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(Guid orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return Results.NotFound("Order not found.");
            }

            if (order.Status != OrderStatus.Pending)
            {
                return Results.BadRequest("Only 'Pending' orders can be cancelled.");
            }

            order.Status = OrderStatus.Cancelled;

            await _context.SaveChangesAsync();

            // Re-load order including items and menuitem
            var updatedOrder = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(oi => oi.MenuItem)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            var response = new DetailedOrderResponse(
                updatedOrder.OrderId,
                updatedOrder.UserId,
                updatedOrder.Status.ToString(),
                updatedOrder.TotalAmount,
                updatedOrder.OrderDate,
                updatedOrder.Items.Select(oi => new OrderItemResponse(
                    oi.MenuItemId,
                    oi.MenuItem != null ? oi.MenuItem.Name : string.Empty,
                    oi.UnitPrice,
                    oi.Quantity)).ToList()
            );

            return Results.Ok(response);
        }
    }
}