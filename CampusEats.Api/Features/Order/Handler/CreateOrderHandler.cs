using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Orders;
using Microsoft.EntityFrameworkCore;

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

            var menuItems = await _context.MenuItems
                .Where(mi => request.MenuItemIds.Contains(mi.MenuItemId))
                .ToListAsync();

            if (menuItems.Count != request.MenuItemIds.Count)
            {
                return Results.BadRequest("One or more menu items are invalid.");
            }

            var totalAmount = menuItems.Sum(mi => mi.Price);

            var order = new Infrastructure.Persistence.Entities.Order
            {
                OrderId = Guid.NewGuid(),
                UserId = request.UserId,
                Items = menuItems,
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
                menuItems.Select(item => new OrderItemResponse(item.MenuItemId, item.Name, item.Price)).ToList()
            );

            return Results.Created($"/orders/{order.OrderId}", response);
        }
    }
}