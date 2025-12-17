namespace CampusEats.Api.Features.User.Response;

public record UserResponse(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    int? LoyaltyPoints,
    DateTime CreatedAt,
    int TotalOrders
);