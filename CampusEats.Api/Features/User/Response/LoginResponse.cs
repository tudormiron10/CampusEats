namespace CampusEats.Api.Features.User.Response;

public record LoginResponse(
    string Token, 
    UserResponse User 
);