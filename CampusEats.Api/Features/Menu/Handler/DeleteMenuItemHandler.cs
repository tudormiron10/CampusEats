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
        var item = await _context.MenuItems.FindAsync(menuItemId);
        if (item == null)
        {
            return Results.NotFound("Menu item not found.");
        }
        
        // Verificăm dacă articolul este folosit într-o comandă
        var isUsedInOrder = await _context.Orders.AnyAsync(o => o.Items.Contains(item));
        if (isUsedInOrder)
        {
            return Results.BadRequest("Cannot delete menu item as it is part of an existing order.");
        }

        _context.MenuItems.Remove(item);
        await _context.SaveChangesAsync();
        return Results.NoContent();
    }
}