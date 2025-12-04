using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class CreateOfferHandler : IRequestHandler<CreateOfferRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public CreateOfferHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(CreateOfferRequest request, CancellationToken cancellationToken)
    {
        if (!request.HttpContext.IsStaff())
            return Results.Forbid();

        LoyaltyTier? minimumTier = null;
        if (!string.IsNullOrEmpty(request.MinimumTier))
        {
            if (!Enum.TryParse<LoyaltyTier>(request.MinimumTier, out var parsedTier))
                return ApiErrors.ValidationFailed($"Invalid tier: {request.MinimumTier}. Valid values: Bronze, Silver, Gold.");
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

        var offer = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            PointCost = request.PointCost,
            MinimumTier = minimumTier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Items = request.Items.Select(i => new OfferItem
            {
                OfferItemId = Guid.NewGuid(),
                MenuItemId = i.MenuItemId,
                Quantity = i.Quantity
            }).ToList()
        };

        _context.Offers.Add(offer);
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

        return Results.Created($"/loyalty/offers/{offer.OfferId}", response);
    }
}
