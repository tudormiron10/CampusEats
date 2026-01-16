using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
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
        var menuItem = await _context.MenuItems.FindAsync(new object[] { request.MenuItemId }, cancellationToken);
        if (menuItem == null)
            return ApiErrors.MenuItemNotFound();

        // Block deletion if item is in an active order (Pending, InPreparation, Ready)
        var activeStatuses = new[]
        {
            Infrastructure.Persistence.Entities.OrderStatus.Pending,
            Infrastructure.Persistence.Entities.OrderStatus.InPreparation,
            Infrastructure.Persistence.Entities.OrderStatus.Ready
        };

        var usedInActiveOrders = await _context.OrderItems
            .AnyAsync(oi => oi.MenuItemId == request.MenuItemId
                         && activeStatuses.Contains(oi.Order.Status), cancellationToken);

        if (usedInActiveOrders)
            return ApiErrors.Conflict("Cannot delete menu item as it is part of an active order.");

        // Block deletion if item is in an active offer
        var usedInActiveOffers = await _context.OfferItems
            .AnyAsync(oi => oi.MenuItemId == request.MenuItemId
                         && oi.Offer.IsActive, cancellationToken);

        if (usedInActiveOffers)
            return ApiErrors.Conflict("Cannot delete menu item as it is part of an active offer. Please deactivate the offer first.");

        // Set MenuItemId to null in all order items referencing this menu item
        // This preserves order history (UnitPrice is already stored) while allowing physical delete
        var orderItemsToUpdate = await _context.OrderItems
            .Where(oi => oi.MenuItemId == request.MenuItemId)
            .ToListAsync(cancellationToken);

        foreach (var orderItem in orderItemsToUpdate)
        {
            orderItem.MenuItemId = null;
        }

        // Set MenuItemId to null in all inactive offer items referencing this menu item
        var offerItemsToUpdate = await _context.OfferItems
            .Where(oi => oi.MenuItemId == request.MenuItemId && !oi.Offer.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var offerItem in offerItemsToUpdate)
        {
            offerItem.MenuItemId = null;
        }

        // Now safe to delete the menu item
        _context.MenuItems.Remove(menuItem);
        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}