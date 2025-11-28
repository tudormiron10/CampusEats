namespace CampusEats.Api.Features.Menu;

// DTO "plat" pentru răspunsurile API-ului de meniu
public record MenuItemResponse(
    Guid MenuItemId,
    string Name,
    decimal Price,
    string Category,
    string? ImagePath,
    string Description,
    List<DietaryTagDto> DietaryTags,
    bool IsAvailable,
    int SortOrder
);

public record DietaryTagDto(
    Guid DietaryTagId,
    string Name
);