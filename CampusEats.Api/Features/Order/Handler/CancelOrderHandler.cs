using CampusEats.Api.Features.Order.Request;
using CampusEats.Api.Features.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.Orders.Response;

namespace CampusEats.Api.Features.Orders
{
    public class CancelOrderHandler : IRequestHandler<CancelOrderRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;
        private readonly IPublisher _publisher;

        public CancelOrderHandler(CampusEatsDbContext context, IPublisher publisher)
        {
            _context = context;
            _publisher = publisher;
        }

        public async Task<IResult> Handle(CancelOrderRequest request, CancellationToken cancellationToken)
        {
            var orderId = request.OrderId;

            var order = await _context.Orders.FindAsync(new object[] { orderId }, cancellationToken);
            if (order == null)
                return ApiErrors.OrderNotFound();

            if (order.Status != OrderStatus.Pending)
                return ApiErrors.InvalidOperation("Only pending orders can be cancelled.");

            order.Status = OrderStatus.Cancelled;

            await _context.SaveChangesAsync(cancellationToken);

            // Publish notification to notify kitchen staff that order was cancelled via SignalR
            await _publisher.Publish(new OrderCancelledNotification(
                order.OrderId,
                order.UserId,
                DateTime.UtcNow
            ), cancellationToken);

            // Re-load order including items and menuitem
            var updatedOrder = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(oi => oi.MenuItem)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

            var response = new DetailedOrderResponse(
                updatedOrder!.OrderId,
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