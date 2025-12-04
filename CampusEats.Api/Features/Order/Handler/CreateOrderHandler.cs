using MediatR;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Validators.Orders;
using CampusEats.Api.Features.Notifications;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Orders.Response;
using CampusEats.Api.Features.Order.Request;

namespace CampusEats.Api.Features.Orders
{
    public class CreateOrderHandler : IRequestHandler<CreateOrderRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;
        private readonly IPublisher _publisher;

        public CreateOrderHandler(CampusEatsDbContext context, IPublisher publisher)
        {
            _context = context;
            _publisher = publisher;
        }

        public async Task<IResult> Handle(CreateOrderRequest request, CancellationToken cancellationToken)
        {
            var validator = new CreateOrderValidator();
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return ApiErrors.ValidationFailed(validationResult.Errors.First().ErrorMessage);

            var user = await _context.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
            if (user == null)
                return ApiErrors.UserNotFound();

            // Process PAID items
            var paidIds = request.MenuItemIds ?? new List<Guid>();
            var redeemedIds = request.RedeemedMenuItemIds ?? new List<Guid>();
            
            // Combine all IDs to fetch menu items
            var allIds = paidIds.Concat(redeemedIds).Distinct().ToList();
            
            if (!allIds.Any())
                return ApiErrors.ValidationFailed("Order must contain at least one item.");

            var menuItems = await _context.MenuItems
                .Where(mi => allIds.Contains(mi.MenuItemId))
                .ToListAsync(cancellationToken);

            if (menuItems.Count != allIds.Count)
                return ApiErrors.ValidationFailed("One or more menu items are invalid.");

            // Count paid items
            var paidCounts = paidIds
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            // Count redeemed items
            var redeemedCounts = redeemedIds
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            var orderItems = new List<OrderItem>();

            // Add paid items with normal price
            foreach (var mi in menuItems.Where(m => paidCounts.ContainsKey(m.MenuItemId)))
            {
                orderItems.Add(new OrderItem
                {
                    MenuItemId = mi.MenuItemId,
                    MenuItem = mi,
                    Quantity = paidCounts[mi.MenuItemId],
                    UnitPrice = mi.Price
                });
            }

            // Add redeemed items with price 0
            foreach (var mi in menuItems.Where(m => redeemedCounts.ContainsKey(m.MenuItemId)))
            {
                orderItems.Add(new OrderItem
                {
                    MenuItemId = mi.MenuItemId,
                    MenuItem = mi,
                    Quantity = redeemedCounts[mi.MenuItemId],
                    UnitPrice = 0 // FREE - redeemed with loyalty points
                });
            }

            // Total amount only includes paid items (redeemed items have price 0)
            var totalAmount = orderItems.Sum(oi => oi.UnitPrice * oi.Quantity);

            var order = new Infrastructure.Persistence.Entities.Order
            {
                OrderId = Guid.NewGuid(),
                UserId = request.UserId,
                Items = orderItems,
                TotalAmount = totalAmount,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            // Publish notification to notify kitchen staff of new order via SignalR
            await _publisher.Publish(new OrderCreatedNotification(
                order.OrderId,
                order.UserId,
                user.Name,
                order.TotalAmount,
                order.OrderDate,
                order.Items.Select(oi => new OrderItemNotification(
                    oi.MenuItemId,
                    oi.MenuItem?.Name ?? string.Empty,
                    oi.Quantity,
                    oi.UnitPrice
                )).ToList()
            ), cancellationToken);

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

            return Results.Created($"/orders/{order.OrderId}", response);
        }
    }
}