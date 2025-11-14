using MediatR;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Features.Menu.Request;

public record UpdateMenuItemRequest(
    Guid MenuItemId,
    string Name,
    decimal Price,
    string Category,
    string? ImagePath,
    string? Description,
    string? DietaryTags,
    bool IsAvailable,
    int SortOrder) : IRequest<IResult>;
