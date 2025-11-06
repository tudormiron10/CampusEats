using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Menu
{
    public class GetMenuHandler
    {
        private readonly CampusEatsDbContext _context;

        public GetMenuHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(GetMenuRequest request)
        {
            var query = _context.MenuItems.AsNoTracking();

            if (!string.IsNullOrEmpty(request.Category))
            {
                query = query.Where(mi => mi.Category == request.Category);
            }

            if (!string.IsNullOrEmpty(request.DietaryKeyword))
            {
                query = query.Where(mi => EF.Functions.ILike(mi.Description, $"%{request.DietaryKeyword}%"));
            }

            var menuItems = await query
                .Select(m => new MenuItemResponse(m.MenuItemId, m.Name, m.Price, m.Category, m.ImageUrl, m.Description))
                .ToListAsync();

            return Results.Ok(menuItems);
        }
    }
}