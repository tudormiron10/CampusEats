namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class Category
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string Icon { get; set; } = "🍽️"; // Emoji icon for the category
    public int SortOrder { get; set; } = 0; // For custom ordering of categories
}
