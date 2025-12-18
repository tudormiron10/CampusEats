namespace CampusEats.Api.Features.User.Response;

public record PaginatedUsersResponse(
    List<AdminUserResponse> Users,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record AdminUserResponse(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    int? LoyaltyPoints,
    DateTime CreatedAt,
    int TotalOrders,
    DateTime? LastOrderDate,
    bool HasActiveOrders
);