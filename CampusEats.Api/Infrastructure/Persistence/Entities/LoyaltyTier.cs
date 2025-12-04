namespace CampusEats.Api.Infrastructure.Persistence.Entities;
/// <summary>
/// Loyalty tier levels based on lifetime points earned.
/// Bronze: 0 - 4,999 pts (10 pts/$1)
/// Silver: 5,000 - 14,999 pts (12 pts/$1)
/// Gold: 15,000+ pts (15 pts/$1)
/// </summary>
public enum LoyaltyTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2
}
