using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Features.DietaryTags.Response;
using CampusEats.Api.Features.DietaryTags.Handler;

namespace CampusEats.Api.Tests.Features.DietaryTags;

public class GetDietaryTagsHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetDietaryTagsHandler _handler = null!;

    public GetDietaryTagsHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetDietaryTagsHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetDietaryTagsHandler(_context);
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

    #region Happy Path Tests

    [Fact]
    public async Task Given_ExistingTags_When_Handle_Then_ReturnsOkWithAllTags()
    {
        // Arrange
        await SeedDietaryTag("Vegetarian");
        await SeedDietaryTag("Vegan");
        await SeedDietaryTag("Gluten-Free");

        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ExistingTags_When_Handle_Then_ReturnsMappedResponses()
    {
        // Arrange
        var tag = await SeedDietaryTag("Dairy-Free");
        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        
        var response = ok!.Value!.First();
        response.DietaryTagId.Should().Be(tag.DietaryTagId);
        response.Name.Should().Be("Dairy-Free");
    }

    [Fact]
    public async Task Given_ExistingTags_When_Handle_Then_ReturnsOrderedByName()
    {
        // Arrange - seed in non-alphabetical order
        await SeedDietaryTag("Vegan");
        await SeedDietaryTag("Dairy-Free");
        await SeedDietaryTag("Gluten-Free");
        await SeedDietaryTag("Nut-Free");

        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        
        var names = ok!.Value!.Select(t => t.Name).ToList();
        names.Should().BeInAscendingOrder();
        names[0].Should().Be("Dairy-Free");
        names[1].Should().Be("Gluten-Free");
        names[2].Should().Be("Nut-Free");
        names[3].Should().Be("Vegan");
    }

    [Fact]
    public async Task Given_SingleTag_When_Handle_Then_ReturnsListWithOneItem()
    {
        // Arrange
        await SeedDietaryTag("Vegetarian");
        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value!.First().Name.Should().Be("Vegetarian");
    }

    #endregion

    #region Empty/Edge Cases

    [Fact]
    public async Task Given_NoTags_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ManyTags_When_Handle_Then_ReturnsAllTags()
    {
        // Arrange
        for (int i = 0; i < 50; i++)
        {
            await SeedDietaryTag($"Tag-{i:D2}");
        }

        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(50);
    }

    [Fact]
    public async Task Given_TagsWithSpecialCharacters_When_Handle_Then_ReturnsCorrectly()
    {
        // Arrange
        await SeedDietaryTag("Nut-Free (Peanut)");
        await SeedDietaryTag("100% Organic");
        await SeedDietaryTag("Low-Fat & Low-Sodium");

        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(3);
        ok.Value!.Should().Contain(t => t.Name == "100% Organic");
        ok.Value.Should().Contain(t => t.Name == "Low-Fat & Low-Sodium");
        ok.Value.Should().Contain(t => t.Name == "Nut-Free (Peanut)");
    }

    [Fact]
    public async Task Given_TagsWithSimilarNames_When_Handle_Then_ReturnsAllDistinctly()
    {
        // Arrange
        await SeedDietaryTag("Vegan");
        await SeedDietaryTag("Vegetarian");
        await SeedDietaryTag("Veggie-Friendly");

        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(3);
        
        var distinctIds = ok.Value!.Select(t => t.DietaryTagId).Distinct();
        distinctIds.Should().HaveCount(3);
    }

    #endregion

    #region Data Integrity Tests

    [Fact]
    public async Task Given_ExistingTags_When_HandleMultipleTimes_Then_ReturnsSameResults()
    {
        // Arrange
        await SeedDietaryTag("Vegetarian");
        await SeedDietaryTag("Vegan");
        var request = new GetDietaryTagsRequest();

        // Act
        var result1 = await _handler.Handle(request, CancellationToken.None);
        var result2 = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok1 = result1 as Ok<List<DietaryTagResponse>>;
        var ok2 = result2 as Ok<List<DietaryTagResponse>>;
        
        ok1!.Value.Should().HaveCount(2);
        ok2!.Value.Should().HaveCount(2);
        ok1.Value!.Select(t => t.Name).Should().BeEquivalentTo(ok2.Value!.Select(t => t.Name));
    }

    [Fact]
    public async Task Given_TagWithUnicodeCharacters_When_Handle_Then_ReturnsCorrectly()
    {
        // Arrange
        await SeedDietaryTag("Café Style");
        await SeedDietaryTag("日本料理"); // Japanese cuisine
        await SeedDietaryTag("Végétarien"); // French with accent

        var request = new GetDietaryTagsRequest();

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<DietaryTagResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ExistingTags_When_Handle_Then_DoesNotModifyDatabase()
    {
        // Arrange
        var tag = await SeedDietaryTag("Vegetarian");
        var originalName = tag.Name;
        var request = new GetDietaryTagsRequest();

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert - reload from database
        _context.Entry(tag).Reload();
        tag.Name.Should().Be(originalName);
    }

    #endregion
}

