namespace CampusEats.Client.Models;

/// <summary>
/// Loyalty tier levels based on lifetime points earned
/// </summary>
public enum LoyaltyTier
{
    Bronze,  // 0 - 4,999 pts (10 pts/$1)
    Silver,  // 5,000 - 14,999 pts (12 pts/$1)
    Gold     // 15,000+ pts (15 pts/$1)
}

/// <summary>
/// User's current loyalty status
/// </summary>
public record LoyaltyStatusResponse(
    int CurrentPoints,
    int LifetimePoints,
    LoyaltyTier Tier,
    int PointsToNextTier,
    int NextTierThreshold
);

/// <summary>
/// A single loyalty transaction (earn or redeem)
/// </summary>
public record LoyaltyTransactionResponse(
    Guid TransactionId,
    DateTime Date,
    string Description,  // "Order #a1b2c3", "Redeemed: Coffee Bundle"
    string Type,         // "Earned", "Redeemed"
    int Points,          // Positive for earned, negative for redeemed
    Guid? OrderId
);

/// <summary>
/// A loyalty offer that users can redeem with points
/// </summary>
public record OfferResponse(
    Guid OfferId,
    string Title,
    string? Description,
    string? ImageUrl,
    int PointCost,
    LoyaltyTier? MinimumTier,  // null = all tiers, Silver = Silver+Gold, Gold = Gold only
    List<OfferItemResponse> Items,
    bool IsActive
);

/// <summary>
/// An item included in an offer
/// </summary>
public record OfferItemResponse(
    Guid? MenuItemId,
    string Name,
    int Quantity
);

/// <summary>
/// Request to create or update an offer (Manager)
/// </summary>
public record CreateOfferRequest(
    string Title,
    string? Description,
    string? ImageUrl,
    int PointCost,
    LoyaltyTier? MinimumTier,
    List<OfferItemRequest> Items
);

/// <summary>
/// An item to include in an offer (for create/update)
/// </summary>
public record OfferItemRequest(
    Guid MenuItemId,
    int Quantity
);

/// <summary>
/// Response from redeeming an offer
/// </summary>
public record RedeemOfferResponse(
    bool Success,
    int RemainingPoints,
    string Message
);


