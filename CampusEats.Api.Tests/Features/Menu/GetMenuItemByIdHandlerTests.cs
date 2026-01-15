﻿using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.Menu;
using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Features.Menu.Handler;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Tests.Features.Menu;

public class GetMenuItemByIdHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetMenuItemByIdHandler _handler = null!;

    public GetMenuItemByIdHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetMenuItemByIdHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetMenuItemByIdHandler(_context);
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

    private async Task<MenuItem> SeedMenuItem(
        string name = "Pizza Margherita",
        decimal price = 25.00m,
        string category = "Pizza",
        bool isAvailable = true)
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Price = price,
            Category = category,
            Description = "Delicious pizza with fresh ingredients",
            ImagePath = "/images/pizza.jpg",
            IsAvailable = isAvailable,
            SortOrder = 1,
            MenuItemDietaryTags = new List<MenuItemDietaryTag>()
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();
        return menuItem;
    }

    private async Task<DietaryTag> SeedDietaryTag(string name = "Vegetarian")
    {
        var tag = new DietaryTag
        {
            DietaryTagId = Guid.NewGuid(),
            Name = name
        };

        _context.DietaryTags.Add(tag);
        await _context.SaveChangesAsync();
        return tag;
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_ExistingMenuItemId_When_Handle_Then_ReturnsOkWithMenuItem()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var request = new GetMenuItemByIdRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.MenuItemId.Should().Be(menuItem.MenuItemId);
        ok.Value.Name.Should().Be("Pizza Margherita");
        ok.Value.Price.Should().Be(25.00m);
        ok.Value.Category.Should().Be("Pizza");
        ok.Value.Description.Should().Be("Delicious pizza with fresh ingredients");
        ok.Value.ImagePath.Should().Be("/images/pizza.jpg");
        ok.Value.IsAvailable.Should().BeTrue();
        ok.Value.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task Given_MenuItemWithDietaryTags_When_Handle_Then_ReturnsOkWithTags()
    {
        // Arrange
        var tag1 = await SeedDietaryTag("Vegetarian");
        var tag2 = await SeedDietaryTag("Gluten-Free");

        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = "Veggie Burger",
            Price = 22.00m,
            Category = "Burgers",
            Description = "Healthy veggie burger",
            IsAvailable = true,
            SortOrder = 1,
            MenuItemDietaryTags = new List<MenuItemDietaryTag>
            {
                new MenuItemDietaryTag { DietaryTagId = tag1.DietaryTagId, DietaryTag = tag1 },
                new MenuItemDietaryTag { DietaryTagId = tag2.DietaryTagId, DietaryTag = tag2 }
            }
        };
        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        var request = new GetMenuItemByIdRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.DietaryTags.Should().HaveCount(2);
        ok.Value.DietaryTags.Select(dt => dt.Name).Should().Contain("Vegetarian");
        ok.Value.DietaryTags.Select(dt => dt.Name).Should().Contain("Gluten-Free");
    }

    [Fact]
    public async Task Given_UnavailableMenuItem_When_Handle_Then_ReturnsOkWithItem()
    {
        // Arrange
        var menuItem = await SeedMenuItem(isAvailable: false);
        var request = new GetMenuItemByIdRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Given_MenuItemWithNullImagePath_When_Handle_Then_ReturnsOkWithNullImage()
    {
        // Arrange
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = "Simple Item",
            Price = 15.00m,
            Category = "Main",
            Description = "Simple item without image",
            ImagePath = null,
            IsAvailable = true,
            SortOrder = 0
        };
        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        var request = new GetMenuItemByIdRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.ImagePath.Should().BeNull();
    }

    [Fact]
    public async Task Given_MenuItemWithEmptyDietaryTags_When_Handle_Then_ReturnsOkWithEmptyTags()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var request = new GetMenuItemByIdRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.DietaryTags.Should().BeEmpty();
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentMenuItemId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new GetMenuItemByIdRequest(nonExistentId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value!.Code.Should().Be("NOT_FOUND");
        notFound.Value.Message.Should().Contain("Menu item");
    }

    [Fact]
    public async Task Given_EmptyGuid_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var request = new GetMenuItemByIdRequest(Guid.Empty);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Given_DeletedMenuItem_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var menuItemId = menuItem.MenuItemId;

        // Delete the item
        _context.MenuItems.Remove(menuItem);
        await _context.SaveChangesAsync();

        var request = new GetMenuItemByIdRequest(menuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task Given_MultipleMenuItems_When_HandleWithSpecificId_Then_ReturnsCorrectItem()
    {
        // Arrange
        var menuItem1 = await SeedMenuItem("Item One", 10.00m, "Cat1");
        var menuItem2 = await SeedMenuItem("Item Two", 20.00m, "Cat2");
        var menuItem3 = await SeedMenuItem("Item Three", 30.00m, "Cat3");

        var request = new GetMenuItemByIdRequest(menuItem2.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().Be("Item Two");
        ok.Value.Price.Should().Be(20.00m);
        ok.Value.Category.Should().Be("Cat2");
    }

    [Fact]
    public async Task Given_MenuItemWithHighPrecisionPrice_When_Handle_Then_ReturnsPrecisePrice()
    {
        // Arrange
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = "Precise Price Item",
            Price = 19.99m,
            Category = "Test",
            Description = "Item with precise price",
            IsAvailable = true,
            SortOrder = 0
        };
        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        var request = new GetMenuItemByIdRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Price.Should().Be(19.99m);
    }

    [Fact]
    public async Task Given_MenuItemWithSpecialCharactersInName_When_Handle_Then_ReturnsCorrectName()
    {
        // Arrange
        var specialName = "Pizza & Pasta (Deluxe) - 50% Off!";
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = specialName,
            Price = 25.00m,
            Category = "Specials",
            Description = "Special item",
            IsAvailable = true,
            SortOrder = 0
        };
        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        var request = new GetMenuItemByIdRequest(menuItem.MenuItemId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().Be(specialName);
    }

    #endregion
}

