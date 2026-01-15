﻿using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.Menu;
using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Features.Menu.Handler;

namespace CampusEats.Api.Tests.Features.Menu;

public class GetMenuHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetMenuHandler _handler = null!;

    public GetMenuHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetMenuHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetMenuHandler(_context);
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

    private async Task<Category> SeedCategory(string name, int sortOrder = 0)
    {
        var category = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = name,
            Icon = "🍽️",
            SortOrder = sortOrder
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    private async Task<MenuItem> SeedMenuItem(
        string name,
        string category,
        decimal price = 25.00m,
        bool isAvailable = true,
        int sortOrder = 0,
        string? description = null)
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Price = price,
            Category = category,
            Description = description ?? $"Description for {name}",
            ImagePath = $"/images/{name.ToLower().Replace(" ", "-")}.jpg",
            IsAvailable = isAvailable,
            SortOrder = sortOrder,
            MenuItemDietaryTags = new List<MenuItemDietaryTag>()
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();
        return menuItem;
    }

    private async Task<DietaryTag> SeedDietaryTag(string name)
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

    private async Task AddDietaryTagToMenuItem(MenuItem menuItem, DietaryTag tag)
    {
        var menuItemDietaryTag = new MenuItemDietaryTag
        {
            MenuItemId = menuItem.MenuItemId,
            DietaryTagId = tag.DietaryTagId,
            DietaryTag = tag
        };

        menuItem.MenuItemDietaryTags.Add(menuItemDietaryTag);
        _context.MenuItemDietaryTags.Add(menuItemDietaryTag);
        await _context.SaveChangesAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_AvailableMenuItems_When_Handle_Then_ReturnsOkWithAllAvailableItems()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedMenuItem("Pizza Margherita", "Pizza", isAvailable: true);
        await SeedMenuItem("Pizza Pepperoni", "Pizza", isAvailable: true);
        await SeedMenuItem("Pizza Unavailable", "Pizza", isAvailable: false);

        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(2);
        ok.Value!.Select(m => m.Name).Should().Contain("Pizza Margherita");
        ok.Value.Select(m => m.Name).Should().Contain("Pizza Pepperoni");
        ok.Value.Select(m => m.Name).Should().NotContain("Pizza Unavailable");
    }

    [Fact]
    public async Task Given_CategoryFilter_When_Handle_Then_ReturnsOnlyItemsInCategory()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedCategory("Burgers");
        await SeedMenuItem("Pizza Margherita", "Pizza");
        await SeedMenuItem("Cheeseburger", "Burgers");
        await SeedMenuItem("Veggie Burger", "Burgers");

        var request = new GetMenuRequest(Category: "Burgers", DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(2);
        ok.Value!.All(m => m.Category == "Burgers").Should().BeTrue();
    }

    [Fact]
    public async Task Given_ItemsWithDietaryTags_When_Handle_Then_ReturnsItemsWithTags()
    {
        // Arrange
        await SeedCategory("Salads");
        var vegTag = await SeedDietaryTag("Vegetarian");
        var gfTag = await SeedDietaryTag("Gluten-Free");

        var salad = await SeedMenuItem("Garden Salad", "Salads");
        await AddDietaryTagToMenuItem(salad, vegTag);
        await AddDietaryTagToMenuItem(salad, gfTag);

        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value!.First().DietaryTags.Should().HaveCount(2);
        ok.Value.First().DietaryTags.Select(dt => dt.Name).Should().Contain("Vegetarian");
        ok.Value.First().DietaryTags.Select(dt => dt.Name).Should().Contain("Gluten-Free");
    }

    [Fact]
    public async Task Given_EmptyMenu_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ItemsAcrossCategories_When_Handle_Then_ReturnsSortedByCategorySortOrder()
    {
        // Arrange
        await SeedCategory("Desserts", sortOrder: 3);
        await SeedCategory("Main Course", sortOrder: 2);
        await SeedCategory("Appetizers", sortOrder: 1);

        await SeedMenuItem("Chocolate Cake", "Desserts");
        await SeedMenuItem("Steak", "Main Course");
        await SeedMenuItem("Soup", "Appetizers");

        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(3);
        
        // Items should be ordered by category sort order
        ok.Value![0].Category.Should().Be("Appetizers");
        ok.Value[1].Category.Should().Be("Main Course");
        ok.Value[2].Category.Should().Be("Desserts");
    }

    [Fact]
    public async Task Given_ItemsWithSortOrder_When_Handle_Then_ReturnsSortedBySortOrder()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedMenuItem("Pizza C", "Pizza", sortOrder: 3);
        await SeedMenuItem("Pizza A", "Pizza", sortOrder: 1);
        await SeedMenuItem("Pizza B", "Pizza", sortOrder: 2);

        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value![0].Name.Should().Be("Pizza A");
        ok.Value[1].Name.Should().Be("Pizza B");
        ok.Value[2].Name.Should().Be("Pizza C");
    }

    #endregion

    #region Filter Tests

    [Fact]
    public async Task Given_DietaryKeywordInDescription_When_Handle_Then_ReturnsMatchingItems()
    {
        // Arrange
        await SeedCategory("Main");
        await SeedMenuItem("Healthy Bowl", "Main", description: "A vegan friendly meal");
        await SeedMenuItem("Meat Lovers", "Main", description: "Loaded with bacon and beef");

        var request = new GetMenuRequest(Category: null, DietaryKeyword: "vegan");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value!.First().Name.Should().Be("Healthy Bowl");
    }

    [Fact]
    public async Task Given_DietaryKeywordInTagName_When_Handle_Then_ReturnsMatchingItems()
    {
        // Arrange
        await SeedCategory("Salads");
        var veganTag = await SeedDietaryTag("Vegan");

        var veganSalad = await SeedMenuItem("Vegan Salad", "Salads", description: "Fresh salad");
        await AddDietaryTagToMenuItem(veganSalad, veganTag);

        await SeedMenuItem("Caesar Salad", "Salads", description: "Classic caesar");

        var request = new GetMenuRequest(Category: null, DietaryKeyword: "Vegan");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value!.First().Name.Should().Be("Vegan Salad");
    }

    [Fact]
    public async Task Given_NonExistentCategory_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedMenuItem("Pizza Margherita", "Pizza");

        var request = new GetMenuRequest(Category: "NonExistent", DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_NonMatchingDietaryKeyword_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedMenuItem("Pizza Margherita", "Pizza", description: "Cheese and tomato");

        var request = new GetMenuRequest(Category: null, DietaryKeyword: "keto");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_BothCategoryAndDietaryKeyword_When_Handle_Then_AppliesBothFilters()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedCategory("Salads");
        var vegTag = await SeedDietaryTag("Vegetarian");

        var vegPizza = await SeedMenuItem("Veggie Pizza", "Pizza", description: "vegetarian pizza");
        await AddDietaryTagToMenuItem(vegPizza, vegTag);

        await SeedMenuItem("Meat Pizza", "Pizza", description: "meat lovers");
        await SeedMenuItem("Garden Salad", "Salads", description: "vegetarian salad");

        var request = new GetMenuRequest(Category: "Pizza", DietaryKeyword: "vegetarian");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value!.First().Name.Should().Be("Veggie Pizza");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task Given_AllItemsUnavailable_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedMenuItem("Pizza 1", "Pizza", isAvailable: false);
        await SeedMenuItem("Pizza 2", "Pizza", isAvailable: false);

        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_CaseSensitiveCategoryFilter_When_Handle_Then_MatchesExactCase()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedMenuItem("Margherita", "Pizza");

        // Note: This test verifies current behavior - category match is case-sensitive
        var request = new GetMenuRequest(Category: "pizza", DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        // Lowercase "pizza" won't match "Pizza" (case-sensitive)
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ItemsWithSameSortOrder_When_Handle_Then_SortsByNameAsSecondary()
    {
        // Arrange
        await SeedCategory("Pizza");
        await SeedMenuItem("Pizza B", "Pizza", sortOrder: 1);
        await SeedMenuItem("Pizza A", "Pizza", sortOrder: 1);
        await SeedMenuItem("Pizza C", "Pizza", sortOrder: 1);

        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        // When sort order is the same, items should be sorted by name
        ok!.Value![0].Name.Should().Be("Pizza A");
        ok.Value[1].Name.Should().Be("Pizza B");
        ok.Value[2].Name.Should().Be("Pizza C");
    }

    [Fact]
    public async Task Given_ItemsWithVariousPrices_When_Handle_Then_ReturnsPrecisePrices()
    {
        // Arrange
        await SeedCategory("Menu");
        await SeedMenuItem("Cheap Item", "Menu", price: 5.99m);
        await SeedMenuItem("Medium Item", "Menu", price: 15.50m);
        await SeedMenuItem("Expensive Item", "Menu", price: 99.99m);

        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(3);
        ok.Value!.Should().Contain(m => m.Price == 5.99m);
        ok.Value.Should().Contain(m => m.Price == 15.50m);
        ok.Value.Should().Contain(m => m.Price == 99.99m);
    }

    [Fact]
    public async Task Given_ItemsWithSpecialCharacters_When_Handle_Then_ReturnsCorrectData()
    {
        // Arrange
        await SeedCategory("Specials");
        await SeedMenuItem("Today's Special (50% Off!)", "Specials", description: "Limited time offer & more");

        var request = new GetMenuRequest(Category: null, DietaryKeyword: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<MenuItemResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value!.First().Name.Should().Be("Today's Special (50% Off!)");
        ok.Value.First().Description.Should().Contain("&");
    }

    #endregion
}

