using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Admin.Handler;
using CampusEats.Api.Features.Admin.Request;
using CampusEats.Api.Features.Admin.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.AdminTests;

public class GetAdminStatsHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetAdminStatsHandler _handler = null!;

    public GetAdminStatsHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetAdminStatsHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetAdminStatsHandler(_context);
    }

    public void Dispose()
    {
        if (_context != null)
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _context = null!;
        }
    }

    #region Helper Methods

    private async Task<UserEntity> SeedUser(UserRole role = UserRole.Client, DateTime? createdAt = null)
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = $"test{Guid.NewGuid():N}@example.com",
            Role = role,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128],
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    private async Task<OrderEntity> SeedOrder(UserEntity user, OrderStatus status, decimal totalAmount, DateTime? orderDate = null)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            OrderDate = orderDate ?? DateTime.UtcNow,
            TotalAmount = totalAmount
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return order;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_NoData_When_Handle_Then_ReturnsZeroCounts()
    {
        // Arrange
        var request = new GetAdminStatsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AdminStatsResponse>;
        ok.Should().NotBeNull();
        ok!.Value.TotalUsers.Should().Be(0);
        ok.Value.AdminCount.Should().Be(0);
        ok.Value.ManagerCount.Should().Be(0);
        ok.Value.ClientCount.Should().Be(0);
        ok.Value.OrdersToday.Should().Be(0);
        ok.Value.RevenueToday.Should().Be(0);
    }

    [Fact]
    public async Task Given_UsersWithDifferentRoles_When_Handle_Then_ReturnsCorrectCounts()
    {
        // Arrange
        await SeedUser(UserRole.Admin);
        await SeedUser(UserRole.Admin);
        await SeedUser(UserRole.Manager);
        await SeedUser(UserRole.Client);
        await SeedUser(UserRole.Client);
        await SeedUser(UserRole.Client);
        var request = new GetAdminStatsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AdminStatsResponse>;
        ok.Should().NotBeNull();
        ok!.Value.TotalUsers.Should().Be(6);
        ok.Value.AdminCount.Should().Be(2);
        ok.Value.ManagerCount.Should().Be(1);
        ok.Value.ClientCount.Should().Be(3);
    }

    [Fact]
    public async Task Given_NewUsersThisWeek_When_Handle_Then_ReturnsCorrectCount()
    {
        // Arrange
        await SeedUser(createdAt: DateTime.UtcNow); // Today
        await SeedUser(createdAt: DateTime.UtcNow.AddDays(-3)); // 3 days ago
        await SeedUser(createdAt: DateTime.UtcNow.AddDays(-10)); // 10 days ago (not in week)
        var request = new GetAdminStatsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AdminStatsResponse>;
        ok.Should().NotBeNull();
        ok!.Value.NewUsersThisWeek.Should().Be(2);
        ok.Value.NewUsersThisMonth.Should().Be(3);
    }

    [Fact]
    public async Task Given_OrdersToday_When_Handle_Then_ReturnsCorrectCount()
    {
        // Arrange
        var user = await SeedUser();
        await SeedOrder(user, OrderStatus.Pending, 10.00m, DateTime.UtcNow);
        await SeedOrder(user, OrderStatus.Completed, 20.00m, DateTime.UtcNow);
        await SeedOrder(user, OrderStatus.Cancelled, 15.00m, DateTime.UtcNow); // Cancelled excluded
        await SeedOrder(user, OrderStatus.Pending, 10.00m, DateTime.UtcNow.AddDays(-1)); // Yesterday
        var request = new GetAdminStatsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AdminStatsResponse>;
        ok.Should().NotBeNull();
        ok!.Value.OrdersToday.Should().Be(2); // Pending + Completed today
    }

    [Fact]
    public async Task Given_CompletedOrdersToday_When_Handle_Then_ReturnsCorrectRevenue()
    {
        // Arrange
        var user = await SeedUser();
        await SeedOrder(user, OrderStatus.Completed, 25.50m, DateTime.UtcNow);
        await SeedOrder(user, OrderStatus.Completed, 14.50m, DateTime.UtcNow);
        await SeedOrder(user, OrderStatus.Pending, 100.00m, DateTime.UtcNow); // Not completed
        await SeedOrder(user, OrderStatus.Completed, 50.00m, DateTime.UtcNow.AddDays(-1)); // Yesterday
        var request = new GetAdminStatsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AdminStatsResponse>;
        ok.Should().NotBeNull();
        ok!.Value.RevenueToday.Should().Be(40.00m); // 25.50 + 14.50
    }

    [Fact]
    public async Task Given_Users_When_Handle_Then_ReturnsUsersByRoleData()
    {
        // Arrange
        await SeedUser(UserRole.Admin);
        await SeedUser(UserRole.Manager);
        await SeedUser(UserRole.Client);
        await SeedUser(UserRole.Client);
        var request = new GetAdminStatsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AdminStatsResponse>;
        ok.Should().NotBeNull();
        ok!.Value.UsersByRole.Should().HaveCount(3);
        ok.Value.UsersByRole.Should().Contain(x => x.Role == "Admin" && x.Count == 1);
        ok.Value.UsersByRole.Should().Contain(x => x.Role == "Manager" && x.Count == 1);
        ok.Value.UsersByRole.Should().Contain(x => x.Role == "Client" && x.Count == 2);
    }

    [Fact]
    public async Task Given_TopCustomers_When_Handle_Then_ReturnsTopCustomersData()
    {
        // Arrange
        var customer1 = await SeedUser(UserRole.Client);
        var customer2 = await SeedUser(UserRole.Client);
        await SeedOrder(customer1, OrderStatus.Completed, 100.00m, DateTime.UtcNow);
        await SeedOrder(customer1, OrderStatus.Completed, 50.00m, DateTime.UtcNow);
        await SeedOrder(customer2, OrderStatus.Completed, 75.00m, DateTime.UtcNow);
        var request = new GetAdminStatsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AdminStatsResponse>;
        ok.Should().NotBeNull();
        ok!.Value.TopCustomers.Should().HaveCount(2);
        ok.Value.TopCustomers[0].TotalSpent.Should().Be(150.00m);
        ok.Value.TopCustomers[0].TotalOrders.Should().Be(2);
        ok.Value.TopCustomers[1].TotalSpent.Should().Be(75.00m);
        ok.Value.TopCustomers[1].TotalOrders.Should().Be(1);
    }

    [Fact]
    public async Task Given_NewUsersOverTime_When_Handle_Then_Returns30DaysOfData()
    {
        // Arrange
        await SeedUser(createdAt: DateTime.UtcNow);
        var request = new GetAdminStatsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AdminStatsResponse>;
        ok.Should().NotBeNull();
        ok!.Value.NewUsersOverTime.Should().HaveCount(31); // 30 days + today
    }

    #endregion
}