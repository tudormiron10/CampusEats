// Features/Menu/DeleteMenuItemHandler.cs
using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Menu;

public class DeleteMenuItemHandler
{
    private readonly CampusEatsDbContext _context;

    public DeleteMenuItemHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(Guid menuItemId)
    {
        var menuItem = await _context.MenuItems.FindAsync(menuItemId);
        if (menuItem == null)
        {
            return Results.NotFound("Menu item not found.");
        }

        // Verificăm dacă articolul este folosit într-o comandă
        var usedInOrders = await _context.OrderItems.AnyAsync(oi => oi.MenuItemId == menuItemId);
        if (usedInOrders)
        {
            return Results.BadRequest("Cannot delete menu item as it is part of an existing order.");
        }

        _context.MenuItems.Remove(menuItem);
        await _context.SaveChangesAsync();

        return Results.NoContent();
    }
}