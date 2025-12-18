using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Admin.Request;
using CampusEats.Api.Features.Admin.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Admin.Handler;

public class GetAdminStatsHandler : IRequestHandler<GetAdminStatsRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetAdminStatsHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetAdminStatsRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);
        var monthAgo = today.AddDays(-30);

        // User counts by role
        var userCounts = await _context.Users
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalUsers = userCounts.Sum(x => x.Count);
        var adminCount = userCounts.FirstOrDefault(x => x.Role == UserRole.Admin)?.Count ?? 0;
        var managerCount = userCounts.FirstOrDefault(x => x.Role == UserRole.Manager)?.Count ?? 0;
        var clientCount = userCounts.FirstOrDefault(x => x.Role == UserRole.Client)?.Count ?? 0;

        // New users this week/month
        var newUsersThisWeek = await _context.Users
            .CountAsync(u => u.CreatedAt >= weekAgo, cancellationToken);

        var newUsersThisMonth = await _context.Users
            .CountAsync(u => u.CreatedAt >= monthAgo, cancellationToken);

        // Orders today (exclude cancelled)
        var ordersToday = await _context.Orders
            .CountAsync(o => o.OrderDate.Date == today && o.Status != OrderStatus.Cancelled, cancellationToken);

        // Revenue today (completed orders only)
        var revenueToday = await _context.Orders
            .Where(o => o.OrderDate.Date == today && o.Status == OrderStatus.Completed)
            .SumAsync(o => o.TotalAmount, cancellationToken);

        // Users by role for chart
        var usersByRole = userCounts
            .Select(x => new UsersByRoleData(x.Role.ToString(), x.Count))
            .ToList();

        // New users over time (last 30 days)
        var newUsersOverTime = await _context.Users
            .Where(u => u.CreatedAt >= monthAgo)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        // Fill in missing days with 0
        var newUsersOverTimeData = new List<NewUsersOverTimeData>();
        for (var date = monthAgo; date <= today; date = date.AddDays(1))
        {
            var count = newUsersOverTime.FirstOrDefault(x => x.Date == date)?.Count ?? 0;
            newUsersOverTimeData.Add(new NewUsersOverTimeData(date.ToString("yyyy-MM-dd"), count));
        }

        // Top 10 customers by total spent (completed orders only)
        var topCustomers = await _context.Users
            .Where(u => u.Role == UserRole.Client)
            .Select(u => new
            {
                u.UserId,
                u.Name,
                u.Email,
                TotalOrders = u.Orders.Count(o => o.Status == OrderStatus.Completed),
                TotalSpent = u.Orders
                    .Where(o => o.Status == OrderStatus.Completed)
                    .Sum(o => o.TotalAmount)
            })
            .Where(x => x.TotalOrders > 0)
            .OrderByDescending(x => x.TotalSpent)
            .Take(10)
            .ToListAsync(cancellationToken);

        var topCustomersData = topCustomers
            .Select(x => new TopCustomerData(x.UserId, x.Name, x.Email, x.TotalOrders, x.TotalSpent))
            .ToList();

        var response = new AdminStatsResponse(
            totalUsers,
            adminCount,
            managerCount,
            clientCount,
            newUsersThisWeek,
            newUsersThisMonth,
            ordersToday,
            revenueToday,
            usersByRole,
            newUsersOverTimeData,
            topCustomersData
        );

        return Results.Ok(response);
    }
}