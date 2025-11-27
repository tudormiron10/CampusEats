using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Validators.Menu;
using MediatR;

namespace CampusEats.Api.Features.Menu.Handler
{
    public class CreateMenuItemHandler : IRequestHandler<CreateMenuItemRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public CreateMenuItemHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(CreateMenuItemRequest request, CancellationToken cancellationToken)
        {
            var validator = new CreateMenuItemValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
                return ApiErrors.ValidationFailed(validationResult.Errors.First().ErrorMessage);

            var menuItem = new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                Name = request.Name,
                Price = request.Price,
                Category = request.Category,
                Description = request.Description,
                ImagePath = request.ImagePath,
                DietaryTags = request.DietaryTags,
                IsAvailable = request.IsAvailable,
                SortOrder = request.SortOrder
            };

            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();

            var response = new MenuItemResponse(
                menuItem.MenuItemId,
                menuItem.Name,
                menuItem.Price,
                menuItem.Category,
                menuItem.ImagePath,
                menuItem.Description,
                menuItem.DietaryTags,
                menuItem.IsAvailable,
                menuItem.SortOrder
            );

            return Results.Created($"/menu/{menuItem.MenuItemId}", response);
        }
    }
}