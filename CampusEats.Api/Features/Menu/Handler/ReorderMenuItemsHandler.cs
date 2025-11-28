using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Menu.Handler;

public class ReorderMenuItemsHandler : IRequestHandler<ReorderMenuItemsRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public ReorderMenuItemsHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(ReorderMenuItemsRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedMenuItemIds == null || request.OrderedMenuItemIds.Count == 0)
            return ApiErrors.ValidationFailed("Menu item IDs are required.");

        var menuItems = await _context.MenuItems
            .Where(m => request.OrderedMenuItemIds.Contains(m.MenuItemId))
            .ToListAsync(cancellationToken);

        if (menuItems.Count != request.OrderedMenuItemIds.Count)
            return ApiErrors.ValidationFailed("Some menu item IDs were not found.");

        // Update sort order based on position in the list
        for (int i = 0; i < request.OrderedMenuItemIds.Count; i++)
        {
            var menuItem = menuItems.First(m => m.MenuItemId == request.OrderedMenuItemIds[i]);
            menuItem.SortOrder = i;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}