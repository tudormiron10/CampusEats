using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Orders;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;

namespace CampusEats.Api.Features.Orders
{
    public class CreateOrderHandler
    {
        private readonly CampusEatsDbContext _context;

        public CreateOrderHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(CreateOrderRequest request)
        {
            var validator = new CreateOrderValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                return Results.NotFound("User not found.");
            }

            var requestedIds = request.MenuItemIds ?? new List<Guid>();
            var distinctIds = requestedIds.Distinct().ToList();

            var menuItems = await _context.MenuItems
                .Where(mi => distinctIds.Contains(mi.MenuItemId))
                .ToListAsync();

            if (menuItems.Count != distinctIds.Count)
            {
                return Results.BadRequest("One or more menu items are invalid.");
            }

            var counts = requestedIds
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            var orderItems = menuItems.Select(mi => new OrderItem
            {
                MenuItemId = mi.MenuItemId,
                MenuItem = mi,
                Quantity = counts.GetValueOrDefault(mi.MenuItemId, 1),
                UnitPrice = mi.Price
            }).ToList();

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

            return Results.Created($"/orders/{order.OrderId}", response);
        }
    }
}