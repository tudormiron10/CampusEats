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

public class CreateMenuItemHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private CreateMenuItemHandler _handler = null!;

    public CreateMenuItemHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private CreateMenuItemHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new CreateMenuItemHandler(_context);
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

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_ReturnsCreatedWithMenuItem()
    {
        // Arrange
        var request = new CreateMenuItemRequest(
            Name: "Pizza Margherita",
            Price: 25.99m,
            Category: "Pizza",
            Description: "Classic Italian pizza with tomato and mozzarella",
            ImagePath: "/images/pizza.jpg",
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 1
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<MenuItemResponse>;
        created.Should().NotBeNull();
        created!.Value.Should().NotBeNull();
        created.Value!.Name.Should().Be("Pizza Margherita");
        created.Value.Price.Should().Be(25.99m);
        created.Value.Category.Should().Be("Pizza");
        created.Value.Description.Should().Be("Classic Italian pizza with tomato and mozzarella");
        created.Value.ImagePath.Should().Be("/images/pizza.jpg");
        created.Value.IsAvailable.Should().BeTrue();
        created.Value.SortOrder.Should().Be(1);

        // Verify persistence
        var savedItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.Name == "Pizza Margherita");
        savedItem.Should().NotBeNull();
        savedItem!.Price.Should().Be(25.99m);
        savedItem.Category.Should().Be("Pizza");
    }

    [Fact]
    public async Task Given_ValidRequestWithDietaryTags_When_Handle_Then_ReturnsCreatedWithTags()
    {
        // Arrange
        var tag1 = new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Vegetarian" };
        var tag2 = new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Gluten-Free" };
        _context.DietaryTags.AddRange(tag1, tag2);
        await _context.SaveChangesAsync();

        var request = new CreateMenuItemRequest(
            Name: "Veggie Salad",
            Price: 18.50m,
            Category: "Salads",
            Description: "Fresh vegetable salad",
            ImagePath: null,
            DietaryTagIds: new List<Guid> { tag1.DietaryTagId, tag2.DietaryTagId },
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<MenuItemResponse>;
        created.Should().NotBeNull();
        created!.Value!.DietaryTags.Should().HaveCount(2);
        created.Value.DietaryTags.Select(dt => dt.Name).Should().Contain("Vegetarian");
        created.Value.DietaryTags.Select(dt => dt.Name).Should().Contain("Gluten-Free");

        // Verify persistence
        var savedItem = await _context.MenuItems
            .Include(m => m.MenuItemDietaryTags)
            .FirstOrDefaultAsync(m => m.Name == "Veggie Salad");
        savedItem.Should().NotBeNull();
        savedItem!.MenuItemDietaryTags.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ValidRequestWithNullOptionalFields_When_Handle_Then_ReturnsCreated()
    {
        // Arrange
        var request = new CreateMenuItemRequest(
            Name: "Simple Dish",
            Price: 10.00m,
            Category: "Main",
            Description: "A simple dish",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: false,
            SortOrder: 5
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<MenuItemResponse>;
        created.Should().NotBeNull();
        created!.Value!.ImagePath.Should().BeNull();
        created.Value.DietaryTags.Should().BeEmpty();
        created.Value.IsAvailable.Should().BeFalse();

        // Verify persistence
        var savedItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.Name == "Simple Dish");
        savedItem.Should().NotBeNull();
        savedItem!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_LocationHeaderContainsMenuItemId()
    {
        // Arrange
        var request = new CreateMenuItemRequest(
            Name: "Burger Deluxe",
            Price: 32.00m,
            Category: "Burgers",
            Description: "Premium beef burger",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<MenuItemResponse>;
        created.Should().NotBeNull();
        created!.Location.Should().Contain("/menu/");
        created.Location.Should().Contain(created.Value!.MenuItemId.ToString());
    }

    #endregion

    #region Validation Failure Tests

    [Fact]
    public async Task Given_EmptyName_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMenuItemRequest(
            Name: "",
            Price: 25.00m,
            Category: "Pizza",
            Description: "Some description",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");
        badRequest.Value.Message.Should().Contain("name");

        // Verify no persistence
        var count = await _context.MenuItems.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NameExceeds100Characters_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var longName = new string('A', 101);
        var request = new CreateMenuItemRequest(
            Name: longName,
            Price: 25.00m,
            Category: "Pizza",
            Description: "Some description",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");
        badRequest.Value.Message.Should().Contain("100");

        // Verify no persistence
        var count = await _context.MenuItems.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_ZeroPrice_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMenuItemRequest(
            Name: "Free Item",
            Price: 0m,
            Category: "Pizza",
            Description: "Some description",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");
        badRequest.Value.Message.Should().Contain("greater than zero");

        // Verify no persistence
        var count = await _context.MenuItems.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NegativePrice_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMenuItemRequest(
            Name: "Negative Price Item",
            Price: -5.00m,
            Category: "Pizza",
            Description: "Some description",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");

        // Verify no persistence
        var count = await _context.MenuItems.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyCategory_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMenuItemRequest(
            Name: "Valid Name",
            Price: 25.00m,
            Category: "",
            Description: "Some description",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");
        badRequest.Value.Message.Should().Contain("Category");

        // Verify no persistence
        var count = await _context.MenuItems.CountAsync();
        count.Should().Be(0);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task Given_NameWith100Characters_When_Handle_Then_ReturnsCreated()
    {
        // Arrange
        var exactName = new string('A', 100);
        var request = new CreateMenuItemRequest(
            Name: exactName,
            Price: 25.00m,
            Category: "Pizza",
            Description: "Some description",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<MenuItemResponse>;
        created.Should().NotBeNull();
        created!.Value!.Name.Should().HaveLength(100);
    }

    [Fact]
    public async Task Given_MinimalValidPrice_When_Handle_Then_ReturnsCreated()
    {
        // Arrange
        var request = new CreateMenuItemRequest(
            Name: "Cheap Item",
            Price: 0.01m,
            Category: "Snacks",
            Description: "Very cheap item",
            ImagePath: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<MenuItemResponse>;
        created.Should().NotBeNull();
        created!.Value!.Price.Should().Be(0.01m);
    }

    [Fact]
    public async Task Given_NonExistentDietaryTagIds_When_Handle_Then_ReturnsCreatedWithEmptyTags()
    {
        // Arrange
        var nonExistentTagId = Guid.NewGuid();
        var request = new CreateMenuItemRequest(
            Name: "Item With Invalid Tags",
            Price: 20.00m,
            Category: "Main",
            Description: "Description",
            ImagePath: null,
            DietaryTagIds: new List<Guid> { nonExistentTagId },
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<MenuItemResponse>;
        created.Should().NotBeNull();
        // Non-existent tags are simply ignored
        created!.Value!.DietaryTags.Should().BeEmpty();
    }

    #endregion
}

