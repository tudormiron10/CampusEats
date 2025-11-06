namespace CampusEats.Api.Features.Users;

// Acesta este DTO-ul pe care îl vom returna din API.
public record UserResponse(
    Guid UserId,
    string Name,
    string Email,
    string Role, 
    int? LoyaltyPoints 
);