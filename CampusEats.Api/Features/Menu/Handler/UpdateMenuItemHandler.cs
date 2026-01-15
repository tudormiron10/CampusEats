using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Validators.Menu;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return ApiErrors.ValidationFailed(validationResult.Errors[0].ErrorMessage);

            var item = await _context.MenuItems
                .Include(m => m.MenuItemDietaryTags)
                .FirstOrDefaultAsync(m => m.MenuItemId == request.MenuItemId, cancellationToken);

            if (item == null)
                return ApiErrors.MenuItemNotFound();

            item.Name = request.Name;
            item.Price = request.Price;
            item.Category = request.Category;
            item.ImagePath = request.ImagePath ?? item.ImagePath;
            item.Description = request.Description ?? item.Description;
            item.IsAvailable = request.IsAvailable;
            item.SortOrder = request.SortOrder;

            // Update dietary tags if provided
            if (request.DietaryTagIds != null)
            {
                // Remove existing tags
                _context.MenuItemDietaryTags.RemoveRange(item.MenuItemDietaryTags);

                // Add new tags
                if (request.DietaryTagIds.Count > 0)
                {
                    var newTags = request.DietaryTagIds.Select(tagId => new MenuItemDietaryTag
                    {
                        MenuItemId = item.MenuItemId,
                        DietaryTagId = tagId
                    }).ToList();

                    await _context.MenuItemDietaryTags.AddRangeAsync(newTags, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Load dietary tags for response
            var dietaryTagIds = request.DietaryTagIds ?? item.MenuItemDietaryTags.Select(mdt => mdt.DietaryTagId).ToList();
            var dietaryTags = await _context.DietaryTags
                .Where(dt => dietaryTagIds.Contains(dt.DietaryTagId))
                .Select(dt => new DietaryTagDto(dt.DietaryTagId, dt.Name))
                .ToListAsync(cancellationToken);

            var response = new MenuItemResponse(
                item.MenuItemId,
                item.Name,
                item.Price,
                item.Category,
                item.ImagePath,
                item.Description,
                dietaryTags,
                item.IsAvailable,
                item.SortOrder
            );

            return Results.Ok(response);
        }
    }
}