namespace CampusEats.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Represents a user's loyalty program membership.
/// Each Client user has exactly one Loyalty record.
/// </summary>
public class Loyalty
{
    public Guid LoyaltyId { get; set; }

    /// <summary>
    /// Spendable points balance. Can be used to redeem offers.
    /// </summary>
    public int CurrentPoints { get; set; }

    /// <summary>
    /// Total points ever earned. Used to calculate tier.
    /// Note: This decreases when points are redeemed!
    /// </summary>
    public int LifetimePoints { get; set; }

    // Navigation properties
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public List<LoyaltyTransaction> Transactions { get; set; } = new();
}