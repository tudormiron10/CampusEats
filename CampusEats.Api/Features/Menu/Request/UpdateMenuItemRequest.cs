using MediatR;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Features.Menu.Request;

public record UpdateMenuItemRequest(
    Guid MenuItemId,
    string Name, 
    decimal Price, 
    string Category, 
    string? ImageUrl, 
    string? Description) : IRequest<IResult>;
