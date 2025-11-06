using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Validators.Menu;

namespace CampusEats.Api.Features.Menu
{
    public class UpdateMenuItemHandler
    {
        private readonly CampusEatsDbContext _context;

        public UpdateMenuItemHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(Guid menuItemId, UpdateMenuItemRequest request)
        {
            var validator = new UpdateMenuItemValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            var item = await _context.MenuItems.FindAsync(menuItemId);
            if (item == null)
            {
                return Results.NotFound("Menu item not found.");
            }

            item.Name = request.Name;
            item.Price = request.Price;
            item.Category = request.Category;
            item.ImageUrl = request.ImageUrl ?? item.ImageUrl;
            item.Description = request.Description ?? item.Description;

            await _context.SaveChangesAsync();

            var response = new MenuItemResponse(
                item.MenuItemId,
                item.Name,
                item.Price,
                item.Category,
                item.ImageUrl,
                item.Description
            );

            return Results.Ok(response);
        }
    }
}