using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Loyalty;

public static class LoyaltyHelpers
{
    public static LoyaltyTier CalculateTier(int lifetimePoints) => lifetimePoints switch
    {
        >= 15000 => LoyaltyTier.Gold,
        >= 5000 => LoyaltyTier.Silver,
        _ => LoyaltyTier.Bronze
    };

    public static int GetEarnRate(LoyaltyTier tier) => tier switch
    {
        LoyaltyTier.Bronze => 10,
        LoyaltyTier.Silver => 12,
        LoyaltyTier.Gold => 15,
        _ => 10
    };

    public static int GetNextTierThreshold(LoyaltyTier tier) => tier switch
    {
        LoyaltyTier.Bronze => 5000,
        LoyaltyTier.Silver => 15000,
        LoyaltyTier.Gold => 15000,
        _ => 5000
    };

    public static int GetPointsToNextTier(int lifetimePoints, LoyaltyTier currentTier)
    {
        if (currentTier == LoyaltyTier.Gold)
            return 0;
        var threshold = GetNextTierThreshold(currentTier);
        return Math.Max(0, threshold - lifetimePoints);
    }

    public static bool MeetsTierRequirement(LoyaltyTier userTier, LoyaltyTier? minimumTier)
    {
        if (minimumTier == null)
            return true;
        return userTier >= minimumTier.Value;
    }
}
