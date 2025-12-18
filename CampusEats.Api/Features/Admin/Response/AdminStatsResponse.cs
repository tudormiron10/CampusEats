namespace CampusEats.Api.Features.Admin.Response;

public record AdminStatsResponse(
    // User stats
    int TotalUsers,
    int AdminCount,
    int ManagerCount,
    int ClientCount,
    int NewUsersThisWeek,
    int NewUsersThisMonth,

    // Order stats (today)
    int OrdersToday,
    decimal RevenueToday,

    // Chart data
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