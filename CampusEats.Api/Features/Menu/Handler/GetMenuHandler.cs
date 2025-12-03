using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace CampusEats.Api.Features.Menu.Handler
{
    public class GetMenuHandler : IRequestHandler<GetMenuRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public GetMenuHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(GetMenuRequest request, CancellationToken cancellationToken)
        {
            var query = _context.MenuItems
                .Include(m => m.MenuItemDietaryTags)
                    .ThenInclude(mdt => mdt.DietaryTag)
                .AsNoTracking();

            // Filter by availability - only show available items
            query = query.Where(mi => mi.IsAvailable);

            if (!string.IsNullOrEmpty(request.Category))
            {
                query = query.Where(mi => mi.Category == request.Category);
            }

            if (!string.IsNullOrEmpty(request.DietaryKeyword))
            {
                // Search in both description and dietary tags
                query = query.Where(mi =>
                    EF.Functions.ILike(mi.Description, $"%{request.DietaryKeyword}%") ||
                    mi.MenuItemDietaryTags.Any(mdt =>
                        EF.Functions.ILike(mdt.DietaryTag.Name, $"%{request.DietaryKeyword}%")));
            }

            var menuItems = await query
                .Join(_context.Categories,
                    mi => mi.Category,
                    c => c.Name,
                    (mi, c) => new { MenuItem = mi, CategorySortOrder = c.SortOrder })
                .OrderBy(x => x.CategorySortOrder)
                .ThenBy(x => x.MenuItem.Category)
                .ThenBy(x => x.MenuItem.SortOrder)
                .ThenBy(x => x.MenuItem.Name)
                .Select(x => new MenuItemResponse(
                    x.MenuItem.MenuItemId,
                    x.MenuItem.Name,
                    x.MenuItem.Price,
                    x.MenuItem.Category,
                    x.MenuItem.ImagePath,
                    x.MenuItem.Description,
                    x.MenuItem.MenuItemDietaryTags.Select(mdt => new DietaryTagDto(
                        mdt.DietaryTagId,
                        mdt.DietaryTag.Name
                    )).ToList(),
                    x.MenuItem.IsAvailable,
                    x.MenuItem.SortOrder))
                .ToListAsync(cancellationToken);

            return Results.Ok(menuItems);
        }
    }
}