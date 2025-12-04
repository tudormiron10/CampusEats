﻿using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Loyalty.Response;

public record LoyaltyStatusResponse(
    int CurrentPoints,
    int LifetimePoints,
    LoyaltyTier Tier,
    int PointsToNextTier,
    int NextTierThreshold
);
