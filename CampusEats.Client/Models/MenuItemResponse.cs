namespace CampusEats.Client.Models;

public record MenuItemResponse(
    Guid MenuItemId,
    string Name,
    decimal Price,
    string Category,
    string? ImagePath,
    string Description,
    List<DietaryTagResponse> DietaryTags,
    bool IsAvailable,
    int SortOrder
);

public record CreateMenuItemRequest(
    string Name,
    decimal Price,
    string Category,
    string Description,
    string? ImagePath,
    List<Guid>? DietaryTagIds,
    bool IsAvailable,
    int SortOrder
);

public record UpdateMenuItemRequest(
    string Name,
    decimal Price,
    string Category,
    string? ImagePath,
    string? Description,
    List<Guid>? DietaryTagIds,
    bool IsAvailable,
    int SortOrder
);