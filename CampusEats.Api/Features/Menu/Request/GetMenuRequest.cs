// Features/Menu/GetMenuRequest.cs
using Microsoft.AspNetCore.Mvc;

namespace CampusEats.Api.Features.Menu;

// Folosim [FromQuery] pentru a lega parametrii din URL
// ex: GET /menu?Category=Supe
public record GetMenuRequest(
    [FromQuery] string? Category, 
    [FromQuery] string? DietaryKeyword); // Pentru filtrarea alergenilor/restricțiilor