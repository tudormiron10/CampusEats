using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Menu.Handler;
using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Tests.Features.MenuTests;

public class ReorderMenuItemsHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private ReorderMenuItemsHandler _handler = null!;

    public ReorderMenuItemsHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private ReorderMenuItemsHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new ReorderMenuItemsHandler(_context);
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

    private async Task<MenuItem> SeedMenuItem(string name, int sortOrder = 0)
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            Price = 9.99m,
            Category = "Main Course",
            IsAvailable = true,
            SortOrder = sortOrder
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        return menuItem;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidMenuItemIds_When_Reorder_Then_ReturnsNoContent()
    {
        // Arrange
        var item1 = await SeedMenuItem("Item 1", 0);
        var item2 = await SeedMenuItem("Item 2", 1);
        var item3 = await SeedMenuItem("Item 3", 2);

        var request = new ReorderMenuItemsRequest(new List<Guid>
        {
            item3.MenuItemId,
            item1.MenuItemId,
            item2.MenuItemId
        });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public async Task Given_ValidMenuItemIds_When_Reorder_Then_UpdatesSortOrder()
    {
        // Arrange
        var item1 = await SeedMenuItem("Item 1", 0);
        var item2 = await SeedMenuItem("Item 2", 1);
        var item3 = await SeedMenuItem("Item 3", 2);

        var request = new ReorderMenuItemsRequest(new List<Guid>
        {
            item3.MenuItemId,  // Should be 0
            item1.MenuItemId,  // Should be 1
            item2.MenuItemId   // Should be 2
        });

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var updatedItem1 = await _context.MenuItems.FindAsync(item1.MenuItemId);
        var updatedItem2 = await _context.MenuItems.FindAsync(item2.MenuItemId);
        var updatedItem3 = await _context.MenuItems.FindAsync(item3.MenuItemId);

        updatedItem3!.SortOrder.Should().Be(0);
        updatedItem1!.SortOrder.Should().Be(1);
        updatedItem2!.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Given_SingleMenuItem_When_Reorder_Then_ReturnsNoContent()
    {
        // Arrange
        var item = await SeedMenuItem("Single Item", 0);

        var request = new ReorderMenuItemsRequest(new List<Guid> { item.MenuItemId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public async Task Given_TwoMenuItems_When_SwapOrder_Then_UpdatesCorrectly()
    {
        // Arrange
        var item1 = await SeedMenuItem("First Item", 0);
        var item2 = await SeedMenuItem("Second Item", 1);

        var request = new ReorderMenuItemsRequest(new List<Guid>
        {
            item2.MenuItemId,  // Should be 0
            item1.MenuItemId   // Should be 1
        });

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var updatedItem1 = await _context.MenuItems.FindAsync(item1.MenuItemId);
        var updatedItem2 = await _context.MenuItems.FindAsync(item2.MenuItemId);

        updatedItem2!.SortOrder.Should().Be(0);
        updatedItem1!.SortOrder.Should().Be(1);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_NullMenuItemIds_When_Reorder_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new ReorderMenuItemsRequest(null!);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_EmptyMenuItemIds_When_Reorder_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new ReorderMenuItemsRequest(new List<Guid>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_NonExistentMenuItemId_When_Reorder_Then_ReturnsBadRequest()
    {
        // Arrange
        var item = await SeedMenuItem("Existing Item", 0);

        var request = new ReorderMenuItemsRequest(new List<Guid>
        {
            item.MenuItemId,
            Guid.NewGuid() // Non-existent
        });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_AllNonExistentMenuItemIds_When_Reorder_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new ReorderMenuItemsRequest(new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid()
        });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    #endregion
}