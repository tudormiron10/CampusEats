using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class RedeemOfferHandler : IRequestHandler<RedeemOfferRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public RedeemOfferHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(RedeemOfferRequest request, CancellationToken cancellationToken)
    {
        var userId = request.HttpContext.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var loyalty = await _context.Loyalties
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (loyalty == null)
            return ApiErrors.NotFound("Loyalty record");

        var offer = await _context.Offers
            .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
            .FirstOrDefaultAsync(o => o.OfferId == request.OfferId, cancellationToken);

        if (offer == null)
            return ApiErrors.NotFound("Offer");

        if (!offer.IsActive)
            return ApiErrors.ValidationFailed("This offer is no longer available.");

        if (loyalty.CurrentPoints < offer.PointCost)
            return ApiErrors.ValidationFailed($"Not enough points. You have {loyalty.CurrentPoints} but need {offer.PointCost}.");

        var userTier = LoyaltyHelpers.CalculateTier(loyalty.LifetimePoints);
        if (!LoyaltyHelpers.MeetsTierRequirement(userTier, offer.MinimumTier))
            return ApiErrors.ValidationFailed($"This offer requires {offer.MinimumTier} tier or higher.");

        loyalty.CurrentPoints -= offer.PointCost;
        loyalty.LifetimePoints -= offer.PointCost;

        var transaction = new LoyaltyTransaction
        {
            TransactionId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            Description = $"Redeemed: {offer.Title}",
            Type = "Redeemed",
            Points = -offer.PointCost,
            OrderId = null,
            LoyaltyId = loyalty.LoyaltyId
        };

        _context.LoyaltyTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            success = true,
            remainingPoints = loyalty.CurrentPoints,
            message = $"Offer redeemed! You received: {string.Join(", ", offer.Items.Select(i => $"{i.Quantity}x {i.MenuItem.Name}"))}"
        });
    }
}
