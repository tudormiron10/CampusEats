namespace CampusEats.Api.Features.Menu;

// DTO "plat" pentru răspunsurile API-ului de meniu
public record MenuItemResponse(
    Guid MenuItemId,
    string Name,
    decimal Price,
    string Category,
    string? ImagePath,
    string Description,
    string? DietaryTags,
    bool IsAvailable,
    int SortOrder
);