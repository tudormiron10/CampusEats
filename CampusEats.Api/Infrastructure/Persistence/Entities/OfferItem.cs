namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class OfferItem
{
    public Guid OfferItemId { get; set; }
    public int Quantity { get; set; }
    public Guid OfferId { get; set; }
    public Offer Offer { get; set; } = null!;
    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
}
