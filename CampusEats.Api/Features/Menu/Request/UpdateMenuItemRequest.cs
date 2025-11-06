// Features/Menu/UpdateMenuItemRequest.cs
namespace CampusEats.Api.Features.Menu;

public record UpdateMenuItemRequest(
    string Name, 
    decimal Price, 
    string Category, 
    string? ImageUrl, 
    string? Description);