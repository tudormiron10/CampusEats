namespace CampusEats.Api.Features.Menu;

// Acesta este DTO-ul (Request-ul) - conține doar date
public record CreateMenuItemRequest(string Name, decimal Price, string Category);