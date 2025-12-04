using CampusEats.Api.Features.Loyalty.Request;
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
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (loyalty == null)
            return ApiErrors.NotFound("Loyalty record");

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
                    i.MenuItem.Name,
                    i.Quantity
                )).ToList(),
                o.IsActive
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(offers);
    }
}
