using Microsoft.AspNetCore.Mvc;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Features.Menu.Request;

public record GetMenuRequest(
    [FromQuery] string? Category, 
    [FromQuery] string? DietaryKeyword) : IRequest<IResult>;
