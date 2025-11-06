using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Menu
{
    public class GetMenuItemByIdHandler
    {
        private readonly CampusEatsDbContext _context;

        public GetMenuItemByIdHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(Guid menuItemId)
        {
            var response = await _context.MenuItems
                .Where(m => m.MenuItemId == menuItemId)
                .Select(m => new MenuItemResponse(m.MenuItemId, m.Name, m.Price, m.Category, m.ImageUrl, m.Description))
                .FirstOrDefaultAsync();

            return response != null ? Results.Ok(response) : Results.NotFound("Menu item not found.");
        }
    }
}