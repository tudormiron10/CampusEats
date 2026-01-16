﻿using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class GetOffersHandler : IRequestHandler<GetOffersRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetOffersHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetOffersRequest request, CancellationToken cancellationToken)
    {
        var userId = request.HttpContext.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var loyalty = await _context.Loyalties
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        // Auto-create loyalty record if it doesn't exist
        if (loyalty == null)
        {
            loyalty = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = userId.Value,
                CurrentPoints = 0,
                LifetimePoints = 0
            };
            _context.Loyalties.Add(loyalty);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var userTier = LoyaltyHelpers.CalculateTier(loyalty.LifetimePoints);

        var offers = await _context.Offers
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
            .Where(o => o.IsActive)
            .Where(o => o.MinimumTier == null || o.MinimumTier <= userTier)
            .OrderBy(o => o.PointCost)
            .Select(o => new OfferResponse(
                o.OfferId,
                o.Title,
                o.Description,
                o.ImageUrl,
                o.PointCost,
                o.MinimumTier,
                o.Items.Select(i => new OfferItemResponse(
                    i.MenuItemId,
                    i.MenuItem != null ? i.MenuItem.Name : "Deleted Item",
                    i.Quantity
                )).ToList(),
                o.IsActive
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(offers);
    }
}
