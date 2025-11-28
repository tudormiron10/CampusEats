using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Validators.Menu;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
                IsAvailable = request.IsAvailable,
                SortOrder = request.SortOrder
            };

            // Add dietary tags if provided
            if (request.DietaryTagIds != null && request.DietaryTagIds.Any())
            {
                var dietaryTags = await _context.DietaryTags
                    .Where(dt => request.DietaryTagIds.Contains(dt.DietaryTagId))
                    .ToListAsync(cancellationToken);

                menuItem.MenuItemDietaryTags = dietaryTags.Select(dt => new MenuItemDietaryTag
                {
                    MenuItemId = menuItem.MenuItemId,
                    DietaryTagId = dt.DietaryTagId
                }).ToList();
            }

            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync(cancellationToken);

            // Load dietary tags for response
            var dietaryTagDtos = menuItem.MenuItemDietaryTags
                .Select(mdt => new DietaryTagDto(mdt.DietaryTagId, mdt.DietaryTag?.Name ?? ""))
                .ToList();

            // If we don't have the names loaded, fetch them
            if (dietaryTagDtos.Any(dt => string.IsNullOrEmpty(dt.Name)))
            {
                var tagIds = menuItem.MenuItemDietaryTags.Select(mdt => mdt.DietaryTagId).ToList();
                var tags = await _context.DietaryTags
                    .Where(dt => tagIds.Contains(dt.DietaryTagId))
                    .ToListAsync(cancellationToken);
                dietaryTagDtos = tags.Select(dt => new DietaryTagDto(dt.DietaryTagId, dt.Name)).ToList();
            }

            var response = new MenuItemResponse(
                menuItem.MenuItemId,
                menuItem.Name,
                menuItem.Price,
                menuItem.Category,
                menuItem.ImagePath,
                menuItem.Description,
                dietaryTagDtos,
                menuItem.IsAvailable,
                menuItem.SortOrder
            );

            return Results.Created($"/menu/{menuItem.MenuItemId}", response);
        }
    }
}