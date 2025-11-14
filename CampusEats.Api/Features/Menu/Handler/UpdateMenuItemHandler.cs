using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Validators.Menu;
using MediatR;

namespace CampusEats.Api.Features.Menu
{
    public class UpdateMenuItemHandler : IRequestHandler<UpdateMenuItemRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public UpdateMenuItemHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(UpdateMenuItemRequest request, CancellationToken cancellationToken)
        {
            var validator = new UpdateMenuItemValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            var item = await _context.MenuItems.FindAsync(request.MenuItemId);
            if (item == null)
            {
                return Results.NotFound("Menu item not found.");
            }

            item.Name = request.Name;
            item.Price = request.Price;
            item.Category = request.Category;
            item.ImagePath = request.ImagePath ?? item.ImagePath;
            item.Description = request.Description ?? item.Description;
            item.DietaryTags = request.DietaryTags ?? item.DietaryTags;
            item.IsAvailable = request.IsAvailable;
            item.SortOrder = request.SortOrder;

            await _context.SaveChangesAsync();

            var response = new MenuItemResponse(
                item.MenuItemId,
                item.Name,
                item.Price,
                item.Category,
                item.ImagePath,
                item.Description,
                item.DietaryTags,
                item.IsAvailable,
                item.SortOrder
            );

            return Results.Ok(response);
        }
    }
}