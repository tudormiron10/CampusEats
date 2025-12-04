﻿using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Loyalty.Response;

public record OfferResponse(
    Guid OfferId,
    string Title,
    string? Description,
    string? ImageUrl,
    int PointCost,
    LoyaltyTier? MinimumTier,
    List<OfferItemResponse> Items,
    bool IsActive
);

public record OfferItemResponse(
    Guid MenuItemId,
    string Name,
    int Quantity
);
