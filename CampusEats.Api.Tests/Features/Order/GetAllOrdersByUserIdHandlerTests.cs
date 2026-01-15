﻿using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using NSubstitute;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.Orders.Requests;
using CampusEats.Api.Features.Order.Handler;
using CampusEats.Api.Features.Orders.Response;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.Order;

public class GetAllOrdersByUserIdHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetAllOrdersByUserIdHandler _handler = null!;
    private ILogger<GetAllOrdersByUserIdHandler> _logger = null!;

    public GetAllOrdersByUserIdHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetAllOrdersByUserIdHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        _logger = Substitute.For<ILogger<GetAllOrdersByUserIdHandler>>();

        return new GetAllOrdersByUserIdHandler(_context, _logger);
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

    private async Task<UserEntity> SeedUser(string name = "Test User", string email = "test@example.com")
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = name,
            Email = email,
            Role = UserRole.Client,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128]
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<OrderEntity> SeedOrder(
        UserEntity user, 
        OrderStatus status = OrderStatus.Pending,
        decimal totalAmount = 50.00m)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            TotalAmount = totalAmount,
            OrderDate = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return order;
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_UserWithOrders_When_Handle_Then_ReturnsOkWithOrders()
    {
        // Arrange
        var user = await SeedUser();
        var order1 = await SeedOrder(user, OrderStatus.Pending, 25.00m);
        var order2 = await SeedOrder(user, OrderStatus.Completed, 50.00m);

        var request = new GetAllOrdersByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(2);
        ok.Value!.Should().Contain(o => o.OrderId == order1.OrderId);
        ok.Value.Should().Contain(o => o.OrderId == order2.OrderId);
    }

    [Fact]
    public async Task Given_UserWithOrders_When_Handle_Then_ReturnsOnlyUserOrders()
    {
        // Arrange
        var user1 = await SeedUser("User 1", "user1@example.com");
        var user2 = await SeedUser("User 2", "user2@example.com");
        
        var order1 = await SeedOrder(user1, OrderStatus.Pending);
        var order2 = await SeedOrder(user2, OrderStatus.Pending);

        var request = new GetAllOrdersByUserIdRequest(user1.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value!.First().UserId.Should().Be((Guid)user1.UserId);
        ok.Value.Should().NotContain(o => o.OrderId == order2.OrderId);
    }

    [Fact]
    public async Task Given_UserWithOrders_When_Handle_Then_MapsToSimpleOrderResponse()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.InPreparation, 75.50m);

        var request = new GetAllOrdersByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        
        var responseOrder = ok!.Value!.First();
        responseOrder.OrderId.Should().Be((Guid)order.OrderId);
        responseOrder.UserId.Should().Be((Guid)user.UserId);
        responseOrder.Status.Should().Be("InPreparation");
        responseOrder.TotalAmount.Should().Be(75.50m);
        responseOrder.OrderDate.Should().BeCloseTo(order.OrderDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Given_UserWithMultipleOrderStatuses_When_Handle_Then_ReturnsAllStatuses()
    {
        // Arrange
        var user = await SeedUser();
        await SeedOrder(user, OrderStatus.Pending);
        await SeedOrder(user, OrderStatus.InPreparation);
        await SeedOrder(user, OrderStatus.Ready);
        await SeedOrder(user, OrderStatus.Completed);
        await SeedOrder(user, OrderStatus.Cancelled);

        var request = new GetAllOrdersByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(5);
        ok.Value!.Select(o => o.Status).Should().Contain(new[] 
        { 
            "Pending", 
            "InPreparation", 
            "Ready", 
            "Completed", 
            "Cancelled" 
        });
    }

    #endregion

    #region Empty/Edge Cases

    [Fact]
    public async Task Given_UserWithNoOrders_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var user = await SeedUser();
        var request = new GetAllOrdersByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_NonExistentUserId_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var request = new GetAllOrdersByUserIdRequest(nonExistentUserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        // Handler returns empty list for non-existent user (not 404)
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_EmptyUserId_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var request = new GetAllOrdersByUserIdRequest(Guid.Empty);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    #endregion

    #region Data Integrity Tests

    [Fact]
    public async Task Given_OrdersWithDifferentTotals_When_Handle_Then_ReturnsPreciseTotals()
    {
        // Arrange
        var user = await SeedUser();
        await SeedOrder(user, OrderStatus.Completed, 19.99m);
        await SeedOrder(user, OrderStatus.Completed, 125.50m);
        await SeedOrder(user, OrderStatus.Completed, 0.01m);

        var request = new GetAllOrdersByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value!.Should().Contain(o => o.TotalAmount == 19.99m);
        ok.Value.Should().Contain(o => o.TotalAmount == 125.50m);
        ok.Value.Should().Contain(o => o.TotalAmount == 0.01m);
    }

    [Fact]
    public async Task Given_ManyOrders_When_Handle_Then_ReturnsAllOrders()
    {
        // Arrange
        var user = await SeedUser();
        const int orderCount = 50;
        
        for (int i = 0; i < orderCount; i++)
        {
            await SeedOrder(user, OrderStatus.Completed, i * 10.00m);
        }

        var request = new GetAllOrdersByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(orderCount);
    }

    [Fact]
    public async Task Given_OrdersFromMultipleUsers_When_Handle_Then_FiltersCorrectly()
    {
        // Arrange
        var user1 = await SeedUser("Alice", "alice@example.com");
        var user2 = await SeedUser("Bob", "bob@example.com");
        var user3 = await SeedUser("Charlie", "charlie@example.com");

        // Create orders for each user
        await SeedOrder(user1, OrderStatus.Pending);
        await SeedOrder(user1, OrderStatus.Completed);
        await SeedOrder(user2, OrderStatus.Pending);
        await SeedOrder(user3, OrderStatus.Ready);
        await SeedOrder(user3, OrderStatus.Completed);
        await SeedOrder(user3, OrderStatus.Cancelled);

        // Act - Get Alice's orders
        var request = new GetAllOrdersByUserIdRequest(user1.UserId);
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<SimpleOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(2);
        ok.Value!.All(o => o.UserId == user1.UserId).Should().BeTrue();
    }

    #endregion
}

