using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.Menu;
using CampusEats.Api.Features.Menu.Request;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Tests.Features.Menu;

public class UpdateMenuItemHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private UpdateMenuItemHandler _handler = null!;

    public UpdateMenuItemHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private UpdateMenuItemHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new UpdateMenuItemHandler(_context);
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

    private async Task<MenuItem> SeedMenuItem(
        string name = "Original Pizza",
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
            Description = "Original description",
            ImagePath = "/images/original.jpg",
            IsAvailable = isAvailable,
            SortOrder = 1,
            MenuItemDietaryTags = new List<MenuItemDietaryTag>()
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();
        return menuItem;
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_ReturnsOkWithUpdatedMenuItem()
    {
        // Arrange
        var existingItem = await SeedMenuItem();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "Updated Pizza",
            Price: 30.00m,
            Category: "Gourmet Pizza",
            ImagePath: "/images/updated.jpg",
            Description: "Updated description",
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 2
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Name.Should().Be("Updated Pizza");
        ok.Value.Price.Should().Be(30.00m);
        ok.Value.Category.Should().Be("Gourmet Pizza");
        ok.Value.Description.Should().Be("Updated description");
        ok.Value.SortOrder.Should().Be(2);

        // Verify persistence
        var savedItem = await _context.MenuItems.FindAsync(existingItem.MenuItemId);
        savedItem.Should().NotBeNull();
        savedItem!.Name.Should().Be("Updated Pizza");
        savedItem.Price.Should().Be(30.00m);
        savedItem.Category.Should().Be("Gourmet Pizza");
    }

    [Fact]
    public async Task Given_ValidRequestWithNewDietaryTags_When_Handle_Then_UpdatesTags()
    {
        // Arrange
        var tag1 = new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Vegan" };
        var tag2 = new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Organic" };
        _context.DietaryTags.AddRange(tag1, tag2);

        var existingItem = await SeedMenuItem();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "Updated Item",
            Price: 20.00m,
            Category: "Health",
            ImagePath: null,
            Description: null,
            DietaryTagIds: new List<Guid> { tag1.DietaryTagId, tag2.DietaryTagId },
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.DietaryTags.Should().HaveCount(2);
        ok.Value.DietaryTags.Select(dt => dt.Name).Should().Contain("Vegan");
        ok.Value.DietaryTags.Select(dt => dt.Name).Should().Contain("Organic");

        // Verify persistence
        var savedItem = await _context.MenuItems
            .Include(m => m.MenuItemDietaryTags)
            .FirstOrDefaultAsync(m => m.MenuItemId == existingItem.MenuItemId);
        savedItem!.MenuItemDietaryTags.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ExistingTagsAndEmptyTagsList_When_Handle_Then_RemovesAllTags()
    {
        // Arrange
        var tag = new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Vegetarian" };
        _context.DietaryTags.Add(tag);
        await _context.SaveChangesAsync();

        var existingItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = "Item With Tags",
            Price = 25.00m,
            Category = "Main",
            Description = "Description",
            IsAvailable = true,
            SortOrder = 0,
            MenuItemDietaryTags = new List<MenuItemDietaryTag>
            {
                new MenuItemDietaryTag { DietaryTagId = tag.DietaryTagId }
            }
        };
        _context.MenuItems.Add(existingItem);
        await _context.SaveChangesAsync();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "Item Without Tags",
            Price: 25.00m,
            Category: "Main",
            ImagePath: null,
            Description: null,
            DietaryTagIds: new List<Guid>(), // Empty list = remove all tags
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.DietaryTags.Should().BeEmpty();

        // Verify persistence
        var savedItem = await _context.MenuItems
            .Include(m => m.MenuItemDietaryTags)
            .FirstOrDefaultAsync(m => m.MenuItemId == existingItem.MenuItemId);
        savedItem!.MenuItemDietaryTags.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_AvailableItemSetToUnavailable_When_Handle_Then_UpdatesAvailability()
    {
        // Arrange
        var existingItem = await SeedMenuItem(isAvailable: true);

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: existingItem.Name,
            Price: existingItem.Price,
            Category: existingItem.Category,
            ImagePath: null,
            Description: null,
            DietaryTagIds: null,
            IsAvailable: false,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.IsAvailable.Should().BeFalse();

        // Verify persistence
        var savedItem = await _context.MenuItems.FindAsync(existingItem.MenuItemId);
        savedItem!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Given_NullOptionalFields_When_Handle_Then_KeepsExistingValues()
    {
        // Arrange
        var existingItem = await SeedMenuItem();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "Updated Name",
            Price: 35.00m,
            Category: "Updated Category",
            ImagePath: null, // Should keep existing
            Description: null, // Should keep existing
            DietaryTagIds: null, // Should not modify tags
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        
        // Verify persistence - optional fields should be preserved
        var savedItem = await _context.MenuItems.FindAsync(existingItem.MenuItemId);
        savedItem!.ImagePath.Should().Be("/images/original.jpg");
        savedItem.Description.Should().Be("Original description");
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentMenuItemId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateMenuItemRequest(
            MenuItemId: nonExistentId,
            Name: "Updated Name",
            Price: 25.00m,
            Category: "Category",
            ImagePath: null,
            Description: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value!.Code.Should().Be("NOT_FOUND");
        notFound.Value.Message.Should().Contain("Menu item");
    }

    [Fact]
    public async Task Given_EmptyMenuItemId_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateMenuItemRequest(
            MenuItemId: Guid.Empty,
            Name: "Updated Name",
            Price: 25.00m,
            Category: "Category",
            ImagePath: null,
            Description: null,
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
    }

    [Fact]
    public async Task Given_EmptyName_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var existingItem = await SeedMenuItem();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "",
            Price: 25.00m,
            Category: "Category",
            ImagePath: null,
            Description: null,
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

        // Verify no changes persisted
        var savedItem = await _context.MenuItems.FindAsync(existingItem.MenuItemId);
        savedItem!.Name.Should().Be("Original Pizza");
    }

    [Fact]
    public async Task Given_NameExceeds100Characters_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var existingItem = await SeedMenuItem();
        var longName = new string('A', 101);

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: longName,
            Price: 25.00m,
            Category: "Category",
            ImagePath: null,
            Description: null,
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
    }

    [Fact]
    public async Task Given_ZeroPrice_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var existingItem = await SeedMenuItem();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "Updated Name",
            Price: 0m,
            Category: "Category",
            ImagePath: null,
            Description: null,
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

        // Verify no changes persisted
        var savedItem = await _context.MenuItems.FindAsync(existingItem.MenuItemId);
        savedItem!.Price.Should().Be(25.00m);
    }

    [Fact]
    public async Task Given_NegativePrice_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var existingItem = await SeedMenuItem();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "Updated Name",
            Price: -10.00m,
            Category: "Category",
            ImagePath: null,
            Description: null,
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
    }

    [Fact]
    public async Task Given_EmptyCategory_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var existingItem = await SeedMenuItem();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "Updated Name",
            Price: 25.00m,
            Category: "",
            ImagePath: null,
            Description: null,
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
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task Given_NameWith100Characters_When_Handle_Then_ReturnsOk()
    {
        // Arrange
        var existingItem = await SeedMenuItem();
        var exactName = new string('B', 100);

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: exactName,
            Price: 25.00m,
            Category: "Category",
            ImagePath: null,
            Description: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().HaveLength(100);
    }

    [Fact]
    public async Task Given_MinimalValidPrice_When_Handle_Then_ReturnsOk()
    {
        // Arrange
        var existingItem = await SeedMenuItem();

        var request = new UpdateMenuItemRequest(
            MenuItemId: existingItem.MenuItemId,
            Name: "Updated Item",
            Price: 0.01m,
            Category: "Category",
            ImagePath: null,
            Description: null,
            DietaryTagIds: null,
            IsAvailable: true,
            SortOrder: 0
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<MenuItemResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Price.Should().Be(0.01m);
    }

    #endregion
}

