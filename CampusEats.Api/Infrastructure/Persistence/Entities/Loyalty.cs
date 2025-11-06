namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class Loyalty
{
    public Guid LoyaltyId { get; set; }
    public int Points { get; set; }

    // Foreign key for the 1-to-1 relationship
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}