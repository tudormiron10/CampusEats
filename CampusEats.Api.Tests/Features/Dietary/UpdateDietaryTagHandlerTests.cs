﻿using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Features.DietaryTags.Response;
using CampusEats.Api.Features.DietaryTags.Handler;

namespace CampusEats.Api.Tests.Features.Dietary;

public class UpdateDietaryTagHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private UpdateDietaryTagHandler _handler = null!;

    public UpdateDietaryTagHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private UpdateDietaryTagHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new UpdateDietaryTagHandler(_context);
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
    public async Task Given_ExistingTag_When_UpdateName_Then_ReturnsOkWithUpdatedTag()
    {
        // Arrange
        var tag = await SeedDietaryTag("Vegetarian");
        var request = new UpdateDietaryTagRequest(tag.DietaryTagId, "Vegan");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<DietaryTagResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().Be("Vegan");
        ok.Value.DietaryTagId.Should().Be(tag.DietaryTagId);

        // Verify persistence
        var savedTag = await _context.DietaryTags.FindAsync(tag.DietaryTagId);
        savedTag!.Name.Should().Be("Vegan");
    }

    [Fact]
    public async Task Given_ExistingTag_When_UpdateWithWhitespace_Then_TrimmedNameIsSaved()
    {
        // Arrange
        var tag = await SeedDietaryTag("Vegetarian");
        var request = new UpdateDietaryTagRequest(tag.DietaryTagId, "  Vegan  ");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<DietaryTagResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().Be("Vegan");

        // Verify persistence
        var savedTag = await _context.DietaryTags.FindAsync(tag.DietaryTagId);
        savedTag!.Name.Should().Be("Vegan");
    }

    [Fact]
    public async Task Given_ExistingTag_When_UpdateToSameName_Then_ReturnsOk()
    {
        // Arrange
        var tag = await SeedDietaryTag("Vegetarian");
        var request = new UpdateDietaryTagRequest(tag.DietaryTagId, "Vegetarian");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<DietaryTagResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().Be("Vegetarian");
    }

    [Fact]
    public async Task Given_MultipleTags_When_UpdateOne_Then_OthersUnchanged()
    {
        // Arrange
        var tag1 = await SeedDietaryTag("Vegetarian");
        var tag2 = await SeedDietaryTag("Gluten-Free");
        var request = new UpdateDietaryTagRequest(tag1.DietaryTagId, "Vegan");

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var savedTag1 = await _context.DietaryTags.FindAsync(tag1.DietaryTagId);
        var savedTag2 = await _context.DietaryTags.FindAsync(tag2.DietaryTagId);
        
        savedTag1!.Name.Should().Be("Vegan");
        savedTag2!.Name.Should().Be("Gluten-Free"); // Unchanged
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentTagId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateDietaryTagRequest(nonExistentId, "Vegan");

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
        var request = new UpdateDietaryTagRequest(Guid.Empty, "Vegan");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_ConflictingTagName_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        var tag1 = await SeedDietaryTag("Vegetarian");
        var tag2 = await SeedDietaryTag("Vegan");
        
        // Try to rename tag1 to "Vegan" which already exists
        var request = new UpdateDietaryTagRequest(tag1.DietaryTagId, "Vegan");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
        conflict!.Value!.Code.Should().Be("CONFLICT");
        conflict.Value.Message.Should().Contain("already exists");

        // Verify original name unchanged
        var savedTag = await _context.DietaryTags.FindAsync(tag1.DietaryTagId);
        savedTag!.Name.Should().Be("Vegetarian");
    }

    [Fact]
    public async Task Given_ConflictingTagNameDifferentCase_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        var tag1 = await SeedDietaryTag("Vegetarian");
        await SeedDietaryTag("Vegan");
        
        // Try to rename tag1 to "VEGAN" (case-insensitive conflict)
        var request = new UpdateDietaryTagRequest(tag1.DietaryTagId, "VEGAN");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Given_TagWithSameNameDifferentCase_When_UpdateToSameName_Then_ReturnsOk()
    {
        // Arrange - updating "vegetarian" to "Vegetarian" should work (same tag)
        var tag = await SeedDietaryTag("vegetarian");
        var request = new UpdateDietaryTagRequest(tag.DietaryTagId, "Vegetarian");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<DietaryTagResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().Be("Vegetarian");
    }

    [Fact]
    public async Task Given_LongName_When_Update_Then_ReturnsOk()
    {
        // Arrange
        var tag = await SeedDietaryTag("Short");
        var longName = "Suitable for people with multiple dietary restrictions";
        var request = new UpdateDietaryTagRequest(tag.DietaryTagId, longName);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<DietaryTagResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().Be(longName);
    }

    [Fact]
    public async Task Given_NameWithSpecialCharacters_When_Update_Then_ReturnsOk()
    {
        // Arrange
        var tag = await SeedDietaryTag("Plain");
        var request = new UpdateDietaryTagRequest(tag.DietaryTagId, "Nut-Free (All Types)");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<DietaryTagResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Name.Should().Be("Nut-Free (All Types)");
    }

    #endregion
}

