namespace CampusEats.Client.Models;

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

public record CreateMenuItemRequest(
    string Name,
    decimal Price,
    string Category,
    string Description,
    string? ImagePath,
    string? DietaryTags,
    bool IsAvailable,
    int SortOrder
);

public record UpdateMenuItemRequest(
    string Name,
    decimal Price,
    string Category,
    string? ImagePath,
    string? Description,
    string? DietaryTags,
    bool IsAvailable,
    int SortOrder
);