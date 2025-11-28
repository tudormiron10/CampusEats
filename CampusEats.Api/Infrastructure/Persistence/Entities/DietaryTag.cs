namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class DietaryTag
{
    public Guid DietaryTagId { get; set; }
    public string Name { get; set; } = null!;

    // Navigation property for many-to-many
    public List<MenuItemDietaryTag> MenuItemDietaryTags { get; set; } = new();
}