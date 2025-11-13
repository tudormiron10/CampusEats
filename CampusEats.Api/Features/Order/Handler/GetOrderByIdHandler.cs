using CampusEats.Api.Features.Order.Request;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Orders.Response;

namespace CampusEats.Api.Features.Orders
{
    public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public GetOrderByIdHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(GetOrderByIdRequest request, CancellationToken cancellationToken)
        {
            var orderId = request.OrderId;

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
                .FirstOrDefaultAsync(cancellationToken);

            if (response == null)
            {
                return Results.NotFound("Order not found.");
            }

            return Results.Ok(response);
        }
    }
}