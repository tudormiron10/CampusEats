using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Orders;

namespace CampusEats.Api.Features.Kitchen
{
    public class ReadyOrderHandler
    {
        private readonly CampusEatsDbContext _context;

        public ReadyOrderHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(Guid orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return Results.NotFound("Order not found.");
            }

            if (order.Status != OrderStatus.InPreparation)
            {
                return Results.BadRequest("Only orders 'In Preparation' can be marked as ready.");
            }

            order.Status = OrderStatus.Ready;

            await _context.SaveChangesAsync();

            var response = new DetailedOrderResponse(
                order.OrderId,
                order.UserId,
                order.Status.ToString(),
                order.TotalAmount,
                order.OrderDate,
                order.Items.Select(item => new OrderItemResponse(item.MenuItemId, item.Name, item.Price)).ToList()
            );

            return Results.Ok(response);
        }
    }
}