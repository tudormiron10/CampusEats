using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

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

            // Re-încărcăm comanda pentru a include articolele
            var updatedOrder = await _context.Orders
                .Include(o => o.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            var response = new DetailedOrderResponse(
                updatedOrder.OrderId,
                updatedOrder.UserId,
                updatedOrder.Status.ToString(),
                updatedOrder.TotalAmount,
                updatedOrder.OrderDate,
                updatedOrder.Items.Select(item => new OrderItemResponse(item.MenuItemId, item.Name, item.Price)).ToList()
            );

            return Results.Ok(response);
        }
    }
}