using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.Order.Request;
using CampusEats.Api.Features.Order.Handler;
using CampusEats.Api.Features.Orders.Response;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.Order;

public class GetAllOrdersHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetAllOrdersHandler _handler = null!;

    public GetAllOrdersHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetAllOrdersHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetAllOrdersHandler(_context);
    }

    public void Dispose()
    {
        if (_context != null)
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _context = null!;
        }
        GC.SuppressFinalize(this);
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
            PasswordHash = new byte[] { 1 },
            PasswordSalt = new byte[] { 2 }
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<OrderEntity> SeedOrder(UserEntity user, OrderStatus status, decimal totalAmount, DateTime orderDate)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            TotalAmount = totalAmount,
            OrderDate = orderDate
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    #endregion

    #region Tests

    [Fact]
    public async Task Given_NoOrders_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var request = new GetAllOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MultipleOrders_When_Handle_Then_ReturnsAllOrders()
    {
        // Arrange
        var user = await SeedUser();
        var order1 = await SeedOrder(user, OrderStatus.Pending, 25.50m, DateTime.UtcNow.AddDays(-1));
        var order2 = await SeedOrder(user, OrderStatus.Completed, 15.00m, DateTime.UtcNow);

        var request = new GetAllOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Should().HaveCount(2);
        ok.Value.Should().Contain(o => o.OrderId == order1.OrderId && o.TotalAmount == 25.50m);
        ok.Value.Should().Contain(o => o.OrderId == order2.OrderId && o.Status == "Completed");
    }

    #endregion
}

