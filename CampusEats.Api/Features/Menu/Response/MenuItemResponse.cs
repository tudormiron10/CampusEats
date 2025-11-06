namespace CampusEats.Api.Features.Menu;

// DTO "plat" pentru răspunsurile API-ului de meniu
public record MenuItemResponse(
    Guid MenuItemId,
    string Name,
    decimal Price,
    string Category,
    string ImageUrl,
    string Description
);