namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class MenuItemDietaryTag
{
    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public Guid DietaryTagId { get; set; }
    public DietaryTag DietaryTag { get; set; } = null!;
}