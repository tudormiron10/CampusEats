namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class User
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public UserRole Role { get; set; }

    // 1-to-1 Relationship
    public Loyalty Loyalty { get; set; } = null!;
    // 1-to-many Relationship
    public List<Order> Orders { get; set; } = new();
}