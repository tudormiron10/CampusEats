using CampusEats.Api.Features.Loyalty.Request;
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
            .Include(o => o.Items)
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
        if (missingItems.Any())
            return ApiErrors.ValidationFailed($"Menu items not found: {string.Join(", ", missingItems)}");

        offer.Title = request.Title;
        offer.Description = request.Description;
        offer.ImageUrl = request.ImageUrl;
        offer.PointCost = request.PointCost;
        offer.MinimumTier = minimumTier;

        _context.OfferItems.RemoveRange(offer.Items);
        offer.Items = request.Items.Select(i => new OfferItem
        {
            OfferItemId = Guid.NewGuid(),
            OfferId = offer.OfferId,
            MenuItemId = i.MenuItemId,
            Quantity = i.Quantity
        }).ToList();

        await _context.SaveChangesAsync(cancellationToken);

        var menuItemNames = await _context.MenuItems
            .Where(m => menuItemIds.Contains(m.MenuItemId))
            .ToDictionaryAsync(m => m.MenuItemId, m => m.Name, cancellationToken);

        var response = new OfferResponse(
            offer.OfferId,
            offer.Title,
            offer.Description,
            offer.ImageUrl,
            offer.PointCost,
            offer.MinimumTier,
            offer.Items.Select(i => new OfferItemResponse(
                i.MenuItemId,
                menuItemNames.GetValueOrDefault(i.MenuItemId, "Unknown"),
                i.Quantity
            )).ToList(),
            offer.IsActive
        );

        return Results.Ok(response);
    }
}
