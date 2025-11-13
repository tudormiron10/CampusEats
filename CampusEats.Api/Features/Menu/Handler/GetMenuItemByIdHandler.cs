using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MediatR;
using CampusEats.Api.Features.Menu.Request;

namespace CampusEats.Api.Features.Menu.Handler
{
    public class GetMenuItemByIdHandler : IRequestHandler<GetMenuItemByIdRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public GetMenuItemByIdHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(GetMenuItemByIdRequest request, CancellationToken cancellationToken)
        {
            var response = await _context.MenuItems
                .Where(m => m.MenuItemId == request.MenuItemId)
                .Select(m => new MenuItemResponse(m.MenuItemId, m.Name, m.Price, m.Category, m.ImageUrl, m.Description))
                .FirstOrDefaultAsync();

            return response != null ? Results.Ok(response) : Results.NotFound("Menu item not found.");
        }
    }
}