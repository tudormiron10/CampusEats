namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class MenuItem
{
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public string Category { get; set; } = null!;
    public string ImageUrl { get; set; } = null!;
    public string Description { get; set; } = null!;

    // Many-to-many relationship
    public List<Order> Orders { get; set; } = new();
}