namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class MenuItem
{
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public string Category { get; set; } = null!;
    public string? ImagePath { get; set; } // Changed from ImageUrl to ImagePath for uploaded images
    public string Description { get; set; } = null!;
    public string? DietaryTags { get; set; } // Comma-separated tags: "Vegetarian,Gluten-Free"
    public bool IsAvailable { get; set; } = true; // For hiding out-of-stock items
    public int SortOrder { get; set; } = 0; // For custom ordering within categories

    // Many-to-many relationship
    public List<Order> Orders { get; set; } = new();
}