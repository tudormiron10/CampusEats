namespace CampusEats.Client.Models;

// Paginated users response
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

// Admin stats response
public record AdminStatsResponse(
    int TotalUsers,
    int AdminCount,
    int ManagerCount,
    int ClientCount,
    int NewUsersThisWeek,
    int NewUsersThisMonth,
    int OrdersToday,
    decimal RevenueToday,
    List<UsersByRoleData> UsersByRole,
    List<NewUsersOverTimeData> NewUsersOverTime,
    List<TopCustomerData> TopCustomers
);

public record UsersByRoleData(string Role, int Count);

public record NewUsersOverTimeData(string Date, int Count);

public record TopCustomerData(
    Guid UserId,
    string Name,
    string Email,
    int TotalOrders,
    decimal TotalSpent
);