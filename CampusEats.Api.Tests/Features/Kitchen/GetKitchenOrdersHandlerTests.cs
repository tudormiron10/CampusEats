using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Kitchen;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Features.Kitchen.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.Kitchen;

public class GetKitchenOrdersHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetKitchenOrdersHandler _handler = null!;

    public GetKitchenOrdersHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetKitchenOrdersHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetKitchenOrdersHandler(_context);
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
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128]
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    private async Task<MenuItem> SeedMenuItem(string name = "Test Item")
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Category = "Test",
            Description = "Test description",
            Price = 10.00m,
            IsAvailable = true,
            SortOrder = 1
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        return menuItem;
    }

    private async Task<OrderEntity> SeedOrder(UserEntity user, OrderStatus status, params MenuItem[] items)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            OrderDate = DateTime.UtcNow,
            TotalAmount = items.Sum(i => i.Price)
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        foreach (var item in items)
        {
            var orderItem = new OrderItem
            {
                OrderItemId = Guid.NewGuid(),
                OrderId = order.OrderId,
                MenuItemId = item.MenuItemId,
                Quantity = 1,
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
    public async Task Given_NoOrders_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_PendingOrder_When_Handle_Then_ReturnsOrder()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var order = await SeedOrder(user, OrderStatus.Pending, menuItem);
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value[0].Id.Should().Be(order.OrderId);
        ok.Value[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Given_InPreparationOrder_When_Handle_Then_ReturnsOrder()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var order = await SeedOrder(user, OrderStatus.InPreparation, menuItem);
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value[0].Status.Should().Be("InPreparation");
    }

    [Fact]
    public async Task Given_ReadyOrder_When_Handle_Then_ReturnsOrder()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var order = await SeedOrder(user, OrderStatus.Ready, menuItem);
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value[0].Status.Should().Be("Ready");
    }

    [Fact]
    public async Task Given_CompletedOrder_When_Handle_Then_ReturnsOrder()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var order = await SeedOrder(user, OrderStatus.Completed, menuItem);
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value[0].Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Given_OrderWithItems_When_Handle_Then_ReturnsOrderWithItemDetails()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem("Test Pizza");
        var order = await SeedOrder(user, OrderStatus.Pending, menuItem);
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value[0].Items.Should().HaveCount(1);
        ok.Value[0].Items[0].Name.Should().Be("Test Pizza");
        ok.Value[0].Items[0].Quantity.Should().Be(1);
        ok.Value[0].Items[0].UnitPrice.Should().Be(10.00m);
    }

    [Fact]
    public async Task Given_MultipleActiveOrders_When_Handle_Then_ReturnsAllActiveOrders()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        await SeedOrder(user, OrderStatus.Pending, menuItem);
        await SeedOrder(user, OrderStatus.InPreparation, menuItem);
        await SeedOrder(user, OrderStatus.Ready, menuItem);
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(3);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_CancelledOrder_When_Handle_Then_ExcludesFromResults()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        await SeedOrder(user, OrderStatus.Cancelled, menuItem);
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MixedStatusOrders_When_Handle_Then_OnlyReturnsActiveOrders()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        await SeedOrder(user, OrderStatus.Pending, menuItem);
        await SeedOrder(user, OrderStatus.Cancelled, menuItem);
        await SeedOrder(user, OrderStatus.Ready, menuItem);
        var request = new GetKitchenOrdersRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<KitchenOrderResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(2);
        ok.Value.Should().OnlyContain(o => o.Status != "Cancelled");
    }

    #endregion
}