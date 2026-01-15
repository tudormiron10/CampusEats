using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Kitchen.Handler;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Features.Kitchen.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.Kitchen;

public class GetAnalyticsHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetAnalyticsHandler _handler = null!;

    public GetAnalyticsHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetAnalyticsHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetAnalyticsHandler(_context);
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

    private async Task<UserEntity> SeedUser()
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = $"test{Guid.NewGuid():N}@example.com",
            Role = UserRole.Client,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128]
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    private async Task<MenuItem> SeedMenuItem(string name = "Test Item", string category = "Test")
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Category = category,
            Description = "Test description",
            Price = 10.00m,
            IsAvailable = true,
            SortOrder = 1
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        return menuItem;
    }

    private async Task<OrderEntity> SeedOrder(
        UserEntity user,
        OrderStatus status,
        DateTime orderDate,
        params (MenuItem item, int quantity)[] items)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            OrderDate = DateTime.SpecifyKind(orderDate, DateTimeKind.Utc),
            TotalAmount = items.Sum(i => i.item.Price * i.quantity)
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        foreach (var (item, quantity) in items)
        {
            var orderItem = new OrderItem
            {
                OrderItemId = Guid.NewGuid(),
                OrderId = order.OrderId,
                MenuItemId = item.MenuItemId,
                Quantity = quantity,
                UnitPrice = item.Price
            };
            _context.Set<OrderItem>().Add(orderItem);
        }
        await _context.SaveChangesAsync();

        return order;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_NoOrders_When_GetAnalytics_Then_ReturnsEmptyAnalytics()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var request = new GetAnalyticsRequest(startDate, endDate, "day");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.Summary.TotalOrders.Should().Be(0);
        ok.Value.Summary.TotalRevenue.Should().Be(0);
    }

    [Fact]
    public async Task Given_OrdersInPeriod_When_GetAnalytics_Then_ReturnsSummaryStats()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var orderDate = DateTime.UtcNow.AddDays(-1);
        await SeedOrder(user, OrderStatus.Completed, orderDate, (menuItem, 2));
        await SeedOrder(user, OrderStatus.Completed, orderDate, (menuItem, 1));

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow.AddDays(1);
        var request = new GetAnalyticsRequest(startDate, endDate, "day");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.Summary.TotalOrders.Should().Be(2);
        ok.Value.Summary.TotalRevenue.Should().Be(30m); // (2*10) + (1*10)
        ok.Value.Summary.TotalItemsSold.Should().Be(3);
    }

    [Fact]
    public async Task Given_Orders_When_GetAnalyticsGroupByDay_Then_ReturnsTimeSeriesData()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var orderDate = DateTime.UtcNow.Date;
        await SeedOrder(user, OrderStatus.Completed, orderDate, (menuItem, 1));

        var startDate = DateTime.UtcNow.AddDays(-3).Date;
        var endDate = DateTime.UtcNow.AddDays(1).Date;
        var request = new GetAnalyticsRequest(startDate, endDate, "day");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.TimeSeries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_CompletedAndCancelledOrders_When_GetAnalytics_Then_ReturnsPerformanceMetrics()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var orderDate = DateTime.UtcNow.AddDays(-1);
        await SeedOrder(user, OrderStatus.Completed, orderDate, (menuItem, 1));
        await SeedOrder(user, OrderStatus.Completed, orderDate, (menuItem, 1));
        await SeedOrder(user, OrderStatus.Cancelled, orderDate, (menuItem, 1));

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow.AddDays(1);
        var request = new GetAnalyticsRequest(startDate, endDate, "day");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.Performance.CompletedOrders.Should().Be(2);
        ok.Value.Performance.CancelledOrders.Should().Be(1);
        // Completion rate should be 2/(2+1) = 66.67%
        ok.Value.Performance.CompletionRate.Should().BeApproximately(66.67m, 0.1m);
    }

    [Fact]
    public async Task Given_MultipleItems_When_GetAnalytics_Then_ReturnsItemInsights()
    {
        // Arrange
        var user = await SeedUser();
        var pizza = await SeedMenuItem("Pizza", "Food");
        var burger = await SeedMenuItem("Burger", "Food");
        var orderDate = DateTime.UtcNow.AddDays(-1);
        await SeedOrder(user, OrderStatus.Completed, orderDate, (pizza, 5));
        await SeedOrder(user, OrderStatus.Completed, orderDate, (burger, 2));

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow.AddDays(1);
        var request = new GetAnalyticsRequest(startDate, endDate, "day");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.Items.MostSold.Should().NotBeNull();
        ok.Value.Items.MostSold!.Name.Should().Be("Pizza");
        ok.Value.Items.MostSold.Quantity.Should().Be(5);
        ok.Value.Items.LeastSold!.Name.Should().Be("Burger");
    }

    [Fact]
    public async Task Given_OrdersFromDifferentCustomers_When_GetAnalytics_Then_ReturnsCustomerInsights()
    {
        // Arrange
        var user1 = await SeedUser();
        var user2 = await SeedUser();
        var menuItem = await SeedMenuItem();
        var orderDate = DateTime.UtcNow.AddDays(-1);
        await SeedOrder(user1, OrderStatus.Completed, orderDate, (menuItem, 1));
        await SeedOrder(user2, OrderStatus.Completed, orderDate, (menuItem, 1));

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow.AddDays(1);
        var request = new GetAnalyticsRequest(startDate, endDate, "day");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.Customers.UniqueCustomers.Should().Be(2);
        ok.Value.Customers.OrdersPerCustomer.Should().Be(1);
    }

    [Fact]
    public async Task Given_ReturningCustomer_When_GetAnalytics_Then_IdentifiesReturningVsNew()
    {
        // Arrange
        var returningUser = await SeedUser();
        var newUser = await SeedUser();
        var menuItem = await SeedMenuItem();

        // Returning user has the order before the period
        await SeedOrder(returningUser, OrderStatus.Completed, DateTime.UtcNow.AddDays(-30), (menuItem, 1));

        // Both users have the orders in the period
        var orderDate = DateTime.UtcNow.AddDays(-1);
        await SeedOrder(returningUser, OrderStatus.Completed, orderDate, (menuItem, 1));
        await SeedOrder(newUser, OrderStatus.Completed, orderDate, (menuItem, 1));

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow.AddDays(1);
        var request = new GetAnalyticsRequest(startDate, endDate, "day");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.Customers.ReturningCustomers.Should().Be(1);
        ok.Value.Customers.NewCustomers.Should().Be(1);
    }

    [Fact]
    public async Task Given_Orders_When_GetAnalytics_Then_ReturnsRevenueInsights()
    {
        // Arrange
        var user = await SeedUser();
        var pizza = await SeedMenuItem("Pizza", "Food");
        var drink = await SeedMenuItem("Soda", "Drinks");
        var orderDate = DateTime.UtcNow.AddDays(-1);
        await SeedOrder(user, OrderStatus.Completed, orderDate, (pizza, 3), (drink, 2));

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow.AddDays(1);
        var request = new GetAnalyticsRequest(startDate, endDate, "day");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.Revenue.TopItemsByRevenue.Should().NotBeEmpty();
        ok.Value.Revenue.CategoryBreakdown.Should().NotBeEmpty();
    }

    #endregion

    #region GroupBy Tests

    [Fact]
    public async Task Given_Orders_When_GetAnalyticsGroupByHour_Then_ReturnsHourlyData()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var orderDate = DateTime.UtcNow;
        await SeedOrder(user, OrderStatus.Completed, orderDate, (menuItem, 1));

        var startDate = DateTime.UtcNow.AddHours(-2);
        var endDate = DateTime.UtcNow.AddHours(1);
        var request = new GetAnalyticsRequest(startDate, endDate, "hour");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.TimeSeries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_Orders_When_GetAnalyticsGroupByMonth_Then_ReturnsMonthlyData()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var orderDate = DateTime.UtcNow;
        await SeedOrder(user, OrderStatus.Completed, orderDate, (menuItem, 1));

        var startDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = startDate.AddMonths(2);
        var request = new GetAnalyticsRequest(startDate, endDate, "month");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<AnalyticsResponse>;
        ok.Should().NotBeNull();
        ok.Value!.TimeSeries.Should().NotBeEmpty();
    }

    #endregion
}