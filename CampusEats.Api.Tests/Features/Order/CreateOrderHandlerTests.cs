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

public class CreateOrderHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private CreateOrderHandler _handler = null!;
    private IPublisher _publisher = null!;

    public CreateOrderHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private CreateOrderHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        _publisher = Substitute.For<IPublisher>();

        return new CreateOrderHandler(_context, _publisher);
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

    private async Task<MenuItem> SeedMenuItem(string name = "Pizza", decimal price = 25.00m)
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Price = price,
            Category = "Main",
            Description = $"Delicious {name}",
            IsAvailable = true,
            SortOrder = 0
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();
        return menuItem;
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_ReturnsCreatedWithOrder()
    {
        // Arrange
        var user = await SeedUser();
        var pizza = await SeedMenuItem("Pizza", 25.00m);
        var burger = await SeedMenuItem("Burger", 20.00m);

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { pizza.MenuItemId, burger.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DetailedOrderResponse>;
        created.Should().NotBeNull();
        created!.Value.Should().NotBeNull();
        created.Value!.UserId.Should().Be(user.UserId);
        created.Value.Status.Should().Be("Pending");
        created.Value.TotalAmount.Should().Be(45.00m); // 25 + 20
        created.Value.Items.Should().HaveCount(2);

        // Verify persistence
        var savedOrder = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == user.UserId);
        savedOrder.Should().NotBeNull();
        savedOrder!.Status.Should().Be(OrderStatus.Pending);
        savedOrder.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_DuplicateMenuItemIds_When_Handle_Then_GroupsItemsAndCalculatesCorrectTotal()
    {
        // Arrange
        var user = await SeedUser();
        var pizza = await SeedMenuItem("Pizza", 25.00m);

        // Sending the same pizza 3 times
        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { pizza.MenuItemId, pizza.MenuItemId, pizza.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DetailedOrderResponse>;
        created.Should().NotBeNull();
        created!.Value!.TotalAmount.Should().Be(75.00m); // 25 * 3
        
        // Handler groups duplicates into quantity
        created.Value.Items.Should().HaveCount(1);
        created.Value.Items.First().Quantity.Should().Be(3);

        // Verify persistence - should have 1 OrderItem with Quantity = 3
        var savedOrder = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == user.UserId);
        savedOrder!.Items.Should().HaveCount(1);
        savedOrder.Items.First().Quantity.Should().Be(3);
    }

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_OrderStatusIsPending()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { menuItem.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DetailedOrderResponse>;
        created.Should().NotBeNull();
        created!.Value!.Status.Should().Be("Pending");

        // Verify in database
        var savedOrder = await _context.Orders.FirstOrDefaultAsync();
        savedOrder!.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Given_MixedPaidAndRedeemedItems_When_Handle_Then_CalculatesCorrectTotal()
    {
        // Arrange
        var user = await SeedUser();
        var paidItem = await SeedMenuItem("Paid Pizza", 30.00m);
        var freeItem = await SeedMenuItem("Free Dessert", 15.00m);

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { paidItem.MenuItemId },
            RedeemedMenuItemIds: new List<Guid> { freeItem.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DetailedOrderResponse>;
        created.Should().NotBeNull();
        // Total should only include paid items (redeemed are free)
        created!.Value!.TotalAmount.Should().Be(30.00m);
        created.Value.Items.Should().HaveCount(2);

        // Verify persistence
        var savedOrder = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync();
        
        var paidOrderItem = savedOrder!.Items.First(i => i.MenuItemId == paidItem.MenuItemId);
        var freeOrderItem = savedOrder.Items.First(i => i.MenuItemId == freeItem.MenuItemId);
        
        paidOrderItem.UnitPrice.Should().Be(30.00m);
        freeOrderItem.UnitPrice.Should().Be(0m); // Redeemed = free
    }

    [Fact]
    public async Task Given_OnlyRedeemedItems_When_Handle_Then_TotalIsZero()
    {
        // Arrange
        var user = await SeedUser();
        var freeItem = await SeedMenuItem("Free Item", 20.00m);

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid>(),
            RedeemedMenuItemIds: new List<Guid> { freeItem.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DetailedOrderResponse>;
        created.Should().NotBeNull();
        created!.Value!.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_PublishesNotification()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { menuItem.MenuItemId }
        );

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _publisher.Received(1).Publish(
            Arg.Any<OrderCreatedNotification>(),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentUserId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var nonExistentUserId = Guid.NewGuid();

        var request = new CreateOrderRequest(
            UserId: nonExistentUserId,
            MenuItemIds: new List<Guid> { menuItem.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value!.Code.Should().Be("NOT_FOUND");
        notFound.Value.Message.Should().Contain("User");

        // Verify no order was created
        var orderCount = await _context.Orders.CountAsync();
        orderCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_NonExistentMenuItemId_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var validItem = await SeedMenuItem();
        var invalidItemId = Guid.NewGuid();

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { validItem.MenuItemId, invalidItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");
        badRequest.Value.Message.Should().Contain("menu items");

        // Verify no order was created
        var orderCount = await _context.Orders.CountAsync();
        orderCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyMenuItemIdsList_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid>(),
            RedeemedMenuItemIds: null
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");

        // Verify no order was created
        var orderCount = await _context.Orders.CountAsync();
        orderCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_NullMenuItemIdsList_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: null!,
            RedeemedMenuItemIds: null
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Given_EmptyUserId_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var menuItem = await SeedMenuItem();

        var request = new CreateOrderRequest(
            UserId: Guid.Empty,
            MenuItemIds: new List<Guid> { menuItem.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Given_MultipleItemsWithDifferentQuantities_When_Handle_Then_CalculatesCorrectTotal()
    {
        // Arrange
        var user = await SeedUser();
        var pizza = await SeedMenuItem("Pizza", 25.00m);
        var burger = await SeedMenuItem("Burger", 20.00m);

        // 2 pizzas + 1 burger
        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { pizza.MenuItemId, pizza.MenuItemId, burger.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DetailedOrderResponse>;
        created.Should().NotBeNull();
        created!.Value!.TotalAmount.Should().Be(70.00m); // (25 * 2) + 20

        // Verify item grouping
        var pizzaItem = created.Value.Items.First(i => i.MenuItemId == pizza.MenuItemId);
        var burgerItem = created.Value.Items.First(i => i.MenuItemId == burger.MenuItemId);
        pizzaItem.Quantity.Should().Be(2);
        burgerItem.Quantity.Should().Be(1);
    }

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_LocationHeaderContainsOrderId()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { menuItem.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DetailedOrderResponse>;
        created.Should().NotBeNull();
        created!.Location.Should().Contain("/orders/");
        created.Location.Should().Contain(created.Value!.OrderId.ToString());
    }

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_OrderDateIsSetToUtcNow()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var beforeCreation = DateTime.UtcNow;

        var request = new CreateOrderRequest(
            UserId: user.UserId,
            MenuItemIds: new List<Guid> { menuItem.MenuItemId }
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);
        var afterCreation = DateTime.UtcNow;

        // Assert
        var created = result as Created<DetailedOrderResponse>;
        created.Should().NotBeNull();
        created!.Value!.OrderDate.Should().BeOnOrAfter(beforeCreation);
        created.Value.OrderDate.Should().BeOnOrBefore(afterCreation);
    }

    #endregion
}

