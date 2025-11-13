using MediatR;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Orders.Response;

namespace CampusEats.Api.Features.Kitchen
{
    public class PrepareOrderHandler : IRequestHandler<PrepareOrderRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public PrepareOrderHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(PrepareOrderRequest request, CancellationToken cancellationToken)
        {
            var orderId = request.OrderId;

            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

            if (order == null)
            {
                return Results.NotFound("Order not found.");
            }

            if (order.Status != OrderStatus.Pending)
            {
                return Results.BadRequest("Only 'Pending' orders can be prepared.");
            }

            order.Status = OrderStatus.InPreparation;

            await _context.SaveChangesAsync(cancellationToken);

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