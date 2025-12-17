namespace CampusEats.Client.Models;

public class UserProfileResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? LoyaltyPoints { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalOrders { get; set; }
}