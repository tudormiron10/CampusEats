using MediatR;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Features.Menu.Request;

public record CreateMenuItemRequest(
    string Name,
    decimal Price,
    string Category,
    string Description,
    string? ImagePath,
    List<Guid>? DietaryTagIds,
    bool IsAvailable,
    int SortOrder) : IRequest<IResult>;
