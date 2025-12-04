namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class Offer
{
    public Guid OfferId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int PointCost { get; set; }
    public LoyaltyTier? MinimumTier { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public List<OfferItem> Items { get; set; } = new();
}
