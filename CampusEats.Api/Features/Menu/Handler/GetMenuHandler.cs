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
                .ToListAsync(cancellationToken);

            return Results.Ok(menuItems);
        }
    }
}