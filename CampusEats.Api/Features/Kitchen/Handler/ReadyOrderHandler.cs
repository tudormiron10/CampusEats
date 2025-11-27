using MediatR;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Orders.Response;

namespace CampusEats.Api.Features.Kitchen
{
    public class ReadyOrderHandler : IRequestHandler<ReadyOrderRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public ReadyOrderHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(ReadyOrderRequest request, CancellationToken cancellationToken)
        {
            var orderId = request.OrderId;

            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

            if (order == null)
                return ApiErrors.OrderNotFound();

            if (order.Status != OrderStatus.InPreparation)
                return ApiErrors.InvalidOperation("Only orders 'In Preparation' can be marked as ready.");

            order.Status = OrderStatus.Ready;

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