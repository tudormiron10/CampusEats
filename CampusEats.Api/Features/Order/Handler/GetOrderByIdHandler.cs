using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
                .Where(o => o.OrderId == orderId)
                .Select(o => new DetailedOrderResponse(
                    o.OrderId,
                    o.UserId,
                    o.Status.ToString(),
                    o.TotalAmount,
                    o.OrderDate,
                    o.Items.Select(item => new OrderItemResponse(item.MenuItemId, item.Name, item.Price)).ToList()
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