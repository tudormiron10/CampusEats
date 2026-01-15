﻿using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using MediatR;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.Order.Request;
using CampusEats.Api.Features.Orders;
using CampusEats.Api.Features.Orders.Response;
using CampusEats.Api.Features.Notifications;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.Order;

public class CancelOrderHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private CancelOrderHandler _handler = null!;
    private IPublisher _publisher = null!;

    public CancelOrderHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private CancelOrderHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        _publisher = Substitute.For<IPublisher>();

        return new CancelOrderHandler(_context, _publisher);
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

    private async Task<UserEntity> SeedUser()
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            Role = UserRole.Client,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128]
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<MenuItem> SeedMenuItem()
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = "Pizza",
            Price = 25.00m,
            Category = "Main",
            Description = "Delicious pizza",
            IsAvailable = true,
            SortOrder = 0
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();
        return menuItem;
    }

    private async Task<OrderEntity> SeedOrder(UserEntity user, OrderStatus status = OrderStatus.Pending)
    {
        var menuItem = await SeedMenuItem();
        
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            TotalAmount = 25.00m,
            OrderDate = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var orderItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = order.OrderId,
            MenuItemId = menuItem.MenuItemId,
            Quantity = 1,
            UnitPrice = menuItem.Price
        };

        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync();

        return order;
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_PendingOrder_When_Handle_Then_ReturnsOkWithCancelledStatus()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Pending);
        var request = new CancelOrderRequest(order.OrderId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<DetailedOrderResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Status.Should().Be("Cancelled");
        ok.Value.OrderId.Should().Be(order.OrderId);

        // Verify persistence
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Given_PendingOrder_When_Handle_Then_PublishesNotification()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Pending);
        var request = new CancelOrderRequest(order.OrderId);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _publisher.Received(1).Publish(
            Arg.Any<OrderCancelledNotification>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Given_PendingOrder_When_Handle_Then_PreservesOrderDetails()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Pending);
        var request = new CancelOrderRequest(order.OrderId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<DetailedOrderResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.UserId.Should().Be(user.UserId);
        ok.Value.TotalAmount.Should().Be(25.00m);
        ok.Value.Items.Should().NotBeEmpty();
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentOrderId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentOrderId = Guid.NewGuid();
        var request = new CancelOrderRequest(nonExistentOrderId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value!.Code.Should().Be("NOT_FOUND");
        notFound.Value.Message.Should().Contain("Order");
    }

    [Fact]
    public async Task Given_OrderInPreparation_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.InPreparation);
        var request = new CancelOrderRequest(order.OrderId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("INVALID_OPERATION");
        badRequest.Value.Message.Should().Contain("pending");

        // Verify order status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.InPreparation);
    }

    [Fact]
    public async Task Given_ReadyOrder_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Ready);
        var request = new CancelOrderRequest(order.OrderId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("INVALID_OPERATION");

        // Verify order status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public async Task Given_CompletedOrder_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Completed);
        var request = new CancelOrderRequest(order.OrderId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("INVALID_OPERATION");

        // Verify order status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Given_AlreadyCancelledOrder_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Cancelled);
        var request = new CancelOrderRequest(order.OrderId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("INVALID_OPERATION");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Given_EmptyOrderId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var request = new CancelOrderRequest(Guid.Empty);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_MultiplePendingOrders_When_CancelOne_Then_OtherOrdersUnaffected()
    {
        // Arrange
        var user = await SeedUser();
        var order1 = await SeedOrder(user, OrderStatus.Pending);
        var order2 = await SeedOrder(user, OrderStatus.Pending);
        
        var request = new CancelOrderRequest(order1.OrderId);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var savedOrder1 = await _context.Orders.FindAsync(order1.OrderId);
        var savedOrder2 = await _context.Orders.FindAsync(order2.OrderId);
        
        savedOrder1!.Status.Should().Be(OrderStatus.Cancelled);
        savedOrder2!.Status.Should().Be(OrderStatus.Pending); // Unchanged
    }

    #endregion
}

