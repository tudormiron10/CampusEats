﻿using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class UpdateOfferHandler : IRequestHandler<UpdateOfferRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public UpdateOfferHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(UpdateOfferRequest request, CancellationToken cancellationToken)
    {
        if (!request.HttpContext.IsStaff())
            return Results.Forbid();

        var offer = await _context.Offers
            .FirstOrDefaultAsync(o => o.OfferId == request.OfferId, cancellationToken);

        if (offer == null)
            return ApiErrors.NotFound("Offer");

        LoyaltyTier? minimumTier = null;
        if (!string.IsNullOrEmpty(request.MinimumTier))
        {
            if (!Enum.TryParse<LoyaltyTier>(request.MinimumTier, out var parsedTier))
                return ApiErrors.ValidationFailed($"Invalid tier: {request.MinimumTier}");
            minimumTier = parsedTier;
        }

        var menuItemIds = request.Items.Select(i => i.MenuItemId).ToList();
        var existingMenuItems = await _context.MenuItems
            .Where(m => menuItemIds.Contains(m.MenuItemId))
            .Select(m => m.MenuItemId)
            .ToListAsync(cancellationToken);

        var missingItems = menuItemIds.Except(existingMenuItems).ToList();
        if (missingItems.Count > 0)
            return ApiErrors.ValidationFailed($"Menu items not found: {string.Join(", ", missingItems)}");

        offer.Title = request.Title;
        offer.Description = request.Description;
        offer.ImageUrl = request.ImageUrl;
        offer.PointCost = request.PointCost;
        offer.MinimumTier = minimumTier;

        // Remove existing OfferItems
        var existingOfferItems = await _context.OfferItems
            .Where(oi => oi.OfferId == offer.OfferId)
            .ToListAsync(cancellationToken);
        _context.OfferItems.RemoveRange(existingOfferItems);

        // Create new OfferItem entities and add them to the context
        var newOfferItems = request.Items.Select(i => new OfferItem
        {
            OfferItemId = Guid.NewGuid(),
            OfferId = offer.OfferId,
            MenuItemId = i.MenuItemId,
            Quantity = i.Quantity
        }).ToList();

        await _context.OfferItems.AddRangeAsync(newOfferItems, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Load the offer with items for response
        var offerWithItems = await _context.Offers
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OfferId == offer.OfferId, cancellationToken);

        if (offerWithItems == null)
            return ApiErrors.NotFound("Offer");

        var menuItemNames = await _context.MenuItems
            .Where(m => menuItemIds.Contains(m.MenuItemId))
            .ToDictionaryAsync(m => m.MenuItemId, m => m.Name, cancellationToken);

        var response = new OfferResponse(
            offerWithItems.OfferId,
            offerWithItems.Title,
            offerWithItems.Description,
            offerWithItems.ImageUrl,
            offerWithItems.PointCost,
            offerWithItems.MinimumTier,
            offerWithItems.Items.Select(i => new OfferItemResponse(
                i.MenuItemId,
                i.MenuItemId.HasValue ? menuItemNames.GetValueOrDefault(i.MenuItemId.Value, "Unknown") : "Deleted Item",
                i.Quantity
            )).ToList(),
            offerWithItems.IsActive
        );

        return Results.Ok(response);
    }
}
