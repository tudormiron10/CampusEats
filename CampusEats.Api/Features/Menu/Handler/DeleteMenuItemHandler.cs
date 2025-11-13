// Features/Menu/DeleteMenuItemHandler.cs
using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MediatR;
using CampusEats.Api.Features.Menu.Request;

namespace CampusEats.Api.Features.Menu.Handler;

public class DeleteMenuItemHandler : IRequestHandler<DeleteMenuItemRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public DeleteMenuItemHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(DeleteMenuItemRequest request, CancellationToken cancellationToken)
    {
        var menuItem = await _context.MenuItems.FindAsync(request.MenuItemId);
        if (menuItem == null)
        {
            return Results.NotFound("Menu item not found.");
        }

        // Verificăm dacă articolul este folosit într-o comandă
        var usedInOrders = await _context.OrderItems.AnyAsync(oi => oi.MenuItemId == request.MenuItemId);
        if (usedInOrders)
        {
            return Results.BadRequest("Cannot delete menu item as it is part of an existing order.");
        }

        _context.MenuItems.Remove(menuItem);
        await _context.SaveChangesAsync();

        return Results.NoContent();
    }
}