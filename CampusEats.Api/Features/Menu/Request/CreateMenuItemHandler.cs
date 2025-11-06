using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Menu;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Menu
{
    public class CreateMenuItemHandler
    {
        private readonly CampusEatsDbContext _context;

        public CreateMenuItemHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(CreateMenuItemRequest request)
        {
            var validator = new CreateMenuItemValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            var menuItem = new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                Name = request.Name,
                Price = request.Price,
                Category = request.Category,
                Description = "",
                ImageUrl = ""
            };

            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();

            var response = new MenuItemResponse(
                menuItem.MenuItemId,
                menuItem.Name,
                menuItem.Price,
                menuItem.Category,
                menuItem.ImageUrl,
                menuItem.Description
            );

            return Results.Created($"/menu/{menuItem.MenuItemId}", response);
        }
    }
}