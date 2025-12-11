using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Features.DietaryTags.Handler;

namespace CampusEats.Api.Tests.Features.DietaryTags;

public class DeleteDietaryTagHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private DeleteDietaryTagHandler _handler = null!;

    public DeleteDietaryTagHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private DeleteDietaryTagHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new DeleteDietaryTagHandler(_context);
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

    private async Task<MenuItem> SeedMenuItem(string name = "Garden Salad")
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Price = 12.99m,
            Category = "Salads",
            Description = "Fresh garden salad",
            IsAvailable = true,
            SortOrder = 0
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();
        return menuItem;
    }

    private async Task LinkTagToMenuItem(DietaryTag tag, MenuItem menuItem)
    {
        var link = new MenuItemDietaryTag
        {
            DietaryTagId = tag.DietaryTagId,
            MenuItemId = menuItem.MenuItemId
        };

        _context.MenuItemDietaryTags.Add(link);
        await _context.SaveChangesAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_ExistingTag_When_Delete_Then_ReturnsNoContent()
    {
        // Arrange
        var tag = await SeedDietaryTag("Vegetarian");
        var request = new DeleteDietaryTagRequest(tag.DietaryTagId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify deletion
        var deletedTag = await _context.DietaryTags.FindAsync(tag.DietaryTagId);
        deletedTag.Should().BeNull();
    }

    [Fact]
    public async Task Given_ExistingTag_When_Delete_Then_TagNoLongerInDatabase()
    {
        // Arrange
        var tag = await SeedDietaryTag("Gluten-Free");
        var tagId = tag.DietaryTagId;
        var request = new DeleteDietaryTagRequest(tagId);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var exists = await _context.DietaryTags.AnyAsync(dt => dt.DietaryTagId == tagId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Given_MultipleTags_When_DeleteOne_Then_OthersRemain()
    {
        // Arrange
        var tag1 = await SeedDietaryTag("Vegetarian");
        var tag2 = await SeedDietaryTag("Vegan");
        var tag3 = await SeedDietaryTag("Gluten-Free");
        
        var request = new DeleteDietaryTagRequest(tag2.DietaryTagId);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var remainingTags = await _context.DietaryTags.ToListAsync();
        remainingTags.Should().HaveCount(2);
        remainingTags.Should().Contain(t => t.DietaryTagId == tag1.DietaryTagId);
        remainingTags.Should().Contain(t => t.DietaryTagId == tag3.DietaryTagId);
        remainingTags.Should().NotContain(t => t.DietaryTagId == tag2.DietaryTagId);
    }

    [Fact]
    public async Task Given_UnusedTag_When_Delete_Then_SuccessfullyDeleted()
    {
        // Arrange
        var tag = await SeedDietaryTag("Dairy-Free");
        var menuItem = await SeedMenuItem("Burger"); // MenuItem exists but not linked to tag
        
        var request = new DeleteDietaryTagRequest(tag.DietaryTagId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();
        
        var deletedTag = await _context.DietaryTags.FindAsync(tag.DietaryTagId);
        deletedTag.Should().BeNull();
        
        // MenuItem should still exist
        var existingMenuItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
        existingMenuItem.Should().NotBeNull();
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentTagId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new DeleteDietaryTagRequest(nonExistentId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value!.Code.Should().Be("NOT_FOUND");
        notFound.Value.Message.Should().Contain("Dietary tag");
    }

    [Fact]
    public async Task Given_EmptyTagId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var request = new DeleteDietaryTagRequest(Guid.Empty);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_TagLinkedToMenuItem_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        var tag = await SeedDietaryTag("Vegetarian");
        var menuItem = await SeedMenuItem("Garden Salad");
        await LinkTagToMenuItem(tag, menuItem);
        
        var request = new DeleteDietaryTagRequest(tag.DietaryTagId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
        conflict!.Value!.Code.Should().Be("CONFLICT");
        conflict.Value.Message.Should().Contain("in use");

        // Verify tag was not deleted
        var existingTag = await _context.DietaryTags.FindAsync(tag.DietaryTagId);
        existingTag.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_TagLinkedToMultipleMenuItems_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        var tag = await SeedDietaryTag("Vegan");
        var menuItem1 = await SeedMenuItem("Vegan Burger");
        var menuItem2 = await SeedMenuItem("Vegan Salad");
        await LinkTagToMenuItem(tag, menuItem1);
        await LinkTagToMenuItem(tag, menuItem2);
        
        var request = new DeleteDietaryTagRequest(tag.DietaryTagId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();

        // Verify tag was not deleted
        var existingTag = await _context.DietaryTags.FindAsync(tag.DietaryTagId);
        existingTag.Should().NotBeNull();

        // Verify links still exist
        var linkCount = await _context.MenuItemDietaryTags
            .CountAsync(mdt => mdt.DietaryTagId == tag.DietaryTagId);
        linkCount.Should().Be(2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Given_DeletedTag_When_DeleteAgain_Then_ReturnsNotFound()
    {
        // Arrange
        var tag = await SeedDietaryTag("Temporary");
        var tagId = tag.DietaryTagId;
        
        // First deletion
        await _handler.Handle(new DeleteDietaryTagRequest(tagId), CancellationToken.None);

        // Act - Second deletion attempt
        var result = await _handler.Handle(new DeleteDietaryTagRequest(tagId), CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_TagWithNoLinks_When_Delete_Then_OtherLinksUnaffected()
    {
        // Arrange
        var tag1 = await SeedDietaryTag("Vegetarian");
        var tag2 = await SeedDietaryTag("Vegan");
        var menuItem = await SeedMenuItem("Mixed Salad");
        await LinkTagToMenuItem(tag2, menuItem); // Only tag2 is linked
        
        var request = new DeleteDietaryTagRequest(tag1.DietaryTagId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify tag2's link is still intact
        var linkExists = await _context.MenuItemDietaryTags
            .AnyAsync(mdt => mdt.DietaryTagId == tag2.DietaryTagId);
        linkExists.Should().BeTrue();
    }

    [Fact]
    public async Task Given_OnlyTag_When_Delete_Then_TableIsEmpty()
    {
        // Arrange
        var tag = await SeedDietaryTag("OnlyTag");
        var request = new DeleteDietaryTagRequest(tag.DietaryTagId);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var count = await _context.DietaryTags.CountAsync();
        count.Should().Be(0);
    }

    #endregion
}

