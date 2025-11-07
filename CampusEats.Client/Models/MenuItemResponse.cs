namespace CampusEats.Client.Models;

public record MenuItemResponse(
    Guid MenuItemId,
    string Name,
    decimal Price,
    string Category,
    string? ImageUrl,
    string? Description
);