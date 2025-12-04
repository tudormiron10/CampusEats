﻿using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class GetLoyaltyStatusHandler : IRequestHandler<GetLoyaltyStatusRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetLoyaltyStatusHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetLoyaltyStatusRequest request, CancellationToken cancellationToken)
    {
        var userId = request.HttpContext.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var loyalty = await _context.Loyalties
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (loyalty == null)
            return ApiErrors.NotFound("Loyalty record");

        var tier = LoyaltyHelpers.CalculateTier(loyalty.LifetimePoints);
        var pointsToNextTier = LoyaltyHelpers.GetPointsToNextTier(loyalty.LifetimePoints, tier);
        var nextTierThreshold = LoyaltyHelpers.GetNextTierThreshold(tier);

        var response = new LoyaltyStatusResponse(
            CurrentPoints: loyalty.CurrentPoints,
            LifetimePoints: loyalty.LifetimePoints,
            Tier: tier,
            PointsToNextTier: pointsToNextTier,
            NextTierThreshold: nextTierThreshold
        );

        return Results.Ok(response);
    }
}
