﻿namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class OfferItem
{
    public Guid OfferItemId { get; set; }
    public int Quantity { get; set; }
    public Guid OfferId { get; set; }
    public Offer Offer { get; set; } = null!;
    
    // Nullable to allow menu item deletion while preserving offer history
    public Guid? MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
}
