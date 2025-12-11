using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Features.Menu.Handler;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Tests.Features.Menu;

public class DeleteMenuItemHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private DeleteMenuItemHandler _handler = null!;

    public DeleteMenuItemHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private DeleteMenuItemHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new DeleteMenuItemHandler(_context);
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

    private async Task<MenuItem> SeedMenuItem(string name = "Pizza Margherita")
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Price = 25.00m,
            Category = "Pizza",
            Description = "Classic pizza",
            IsAvailable = true,
            SortOrder = 1
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();
        return menuItem;
    }

    private async Task<Infrastructure.Persistence.Entities.User> SeedUser()
    {
        var user = new Infrastructure.Persistence.Entities.User
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

    private async Task<Infrastructure.Persistence.Entities.Order> SeedOrderWithMenuItem(MenuItem menuItem, CampusEats.Api.Infrastructure.Persistence.Entities.User user, OrderStatus status)
    {
        var orderId = Guid.NewGuid();
        var order = new Infrastructure.Persistence.Entities.Order
        {
            OrderId = orderId,
            UserId = user.UserId,
            Status = status,
            TotalAmount = menuItem.Price,
            OrderDate = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Add OrderItem separately to avoid navigation property issues
        var orderItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
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
    public async Task Given_ExistingMenuItem_When_Handle_Then_ReturnsNoContent()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify deletion
        var deletedItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        deletedItem.Should().BeNull();
    }

    [Fact(Skip = "In-Memory database does not support FK cascade/set-null behavior needed for this test")]
    public async Task Given_MenuItemInCompletedOrder_When_Handle_Then_ReturnsNoContent()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var user = await SeedUser();
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Completed);

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify deletion
        var deletedItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        deletedItem.Should().BeNull();
    }

    [Fact(Skip = "In-Memory database does not support FK cascade/set-null behavior needed for this test")]
    public async Task Given_MenuItemInCancelledOrder_When_Handle_Then_ReturnsNoContent()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var user = await SeedUser();
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Cancelled);

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify deletion
        var deletedItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        deletedItem.Should().BeNull();
    }

    [Fact]
    public async Task Given_MenuItemWithDietaryTags_When_Handle_Then_DeletesItemSuccessfully()
    {
        // Arrange
        var tag = new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Vegetarian" };
        _context.DietaryTags.Add(tag);
        
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = "Veggie Pizza",
            Price = 28.00m,
            Category = "Pizza",
            Description = "Vegetarian pizza",
            IsAvailable = true,
            SortOrder = 1,
            MenuItemDietaryTags = new List<MenuItemDietaryTag>
            {
                new MenuItemDietaryTag { DietaryTagId = tag.DietaryTagId }
            }
        };
        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify deletion
        var deletedItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        deletedItem.Should().BeNull();

        // Dietary tag should still exist
        var existingTag = await _context.DietaryTags.FindAsync(tag.DietaryTagId);
        existingTag.Should().NotBeNull();
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentMenuItemId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new DeleteMenuItemRequest(nonExistentId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value!.Code.Should().Be("NOT_FOUND");
        notFound.Value.Message.Should().Contain("Menu item");
    }

    [Fact]
    public async Task Given_MenuItemInPendingOrder_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var user = await SeedUser();
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Pending);

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
        conflict!.Value!.Code.Should().Be("CONFLICT");
        conflict.Value.Message.Should().Contain("active order");

        // Verify item NOT deleted
        var existingItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        existingItem.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_MenuItemInPreparationOrder_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var user = await SeedUser();
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.InPreparation);

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
        conflict!.Value!.Code.Should().Be("CONFLICT");
        conflict.Value.Message.Should().Contain("active order");

        // Verify item NOT deleted
        var existingItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        existingItem.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_MenuItemInReadyOrder_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var user = await SeedUser();
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Ready);

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
        conflict!.Value!.Code.Should().Be("CONFLICT");

        // Verify item NOT deleted
        var existingItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        existingItem.Should().NotBeNull();
    }

    #endregion

    #region Edge Case Tests

    [Fact(Skip = "In-Memory database does not support FK cascade/set-null behavior needed for this test")]
    public async Task Given_MenuItemInMultipleCompletedOrders_When_Handle_Then_ReturnsNoContent()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var user = await SeedUser();
        
        // Create multiple completed orders
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Completed);
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Completed);
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Cancelled);

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify deletion
        var deletedItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        deletedItem.Should().BeNull();
    }

    [Fact]
    public async Task Given_MenuItemInBothActiveAndCompletedOrders_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var user = await SeedUser();
        
        // One completed, one pending (active)
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Completed);
        await SeedOrderWithMenuItem(menuItem, user, OrderStatus.Pending);

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();

        // Verify item NOT deleted
        var existingItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        existingItem.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_UnavailableMenuItem_When_Handle_Then_ReturnsNoContent()
    {
        // Arrange
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = "Discontinued Item",
            Price = 15.00m,
            Category = "Archive",
            Description = "No longer available",
            IsAvailable = false,
            SortOrder = 99
        };
        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        var request = new DeleteMenuItemRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify deletion
        var deletedItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        deletedItem.Should().BeNull();
    }

    #endregion
}

