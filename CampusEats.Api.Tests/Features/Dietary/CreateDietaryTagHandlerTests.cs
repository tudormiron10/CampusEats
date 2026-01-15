using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Features.DietaryTags.Response;
using CampusEats.Api.Features.DietaryTags.Handler;

namespace CampusEats.Api.Tests.Features.Dietary;

public class CreateDietaryTagHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private CreateDietaryTagHandler _handler = null!;

    public CreateDietaryTagHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private CreateDietaryTagHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new CreateDietaryTagHandler(_context);
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

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_ReturnsCreatedWithTag()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("Gluten-Free");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DietaryTagResponse>;
        created.Should().NotBeNull();
        created!.Value.Should().NotBeNull();
        created.Value!.Name.Should().Be("Gluten-Free");
        created.Value.DietaryTagId.Should().NotBe(Guid.Empty);

        // Verify persistence
        var savedTag = await _context.DietaryTags
            .FirstOrDefaultAsync(dt => dt.Name == "Gluten-Free");
        savedTag.Should().NotBeNull();
        savedTag!.Name.Should().Be("Gluten-Free");
    }

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_LocationHeaderContainsId()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("Vegan");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DietaryTagResponse>;
        created.Should().NotBeNull();
        created!.Location.Should().Contain("/dietary-tags/");
        created.Location.Should().Contain(created.Value!.DietaryTagId.ToString());
    }

    [Fact]
    public async Task Given_NameWithWhitespace_When_Handle_Then_TrimmedNameIsSaved()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("  Dairy-Free  ");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DietaryTagResponse>;
        created.Should().NotBeNull();
        created!.Value!.Name.Should().Be("Dairy-Free");

        // Verify persistence
        var savedTag = await _context.DietaryTags.FirstAsync();
        savedTag.Name.Should().Be("Dairy-Free");
    }

    [Fact]
    public async Task Given_MultipleTags_When_CreateEach_Then_AllArePersisted()
    {
        // Arrange & Act
        await _handler.Handle(new CreateDietaryTagRequest("Vegetarian"), CancellationToken.None);
        await _handler.Handle(new CreateDietaryTagRequest("Vegan"), CancellationToken.None);
        await _handler.Handle(new CreateDietaryTagRequest("Gluten-Free"), CancellationToken.None);

        // Assert
        var count = await _context.DietaryTags.CountAsync();
        count.Should().Be(3);
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_DuplicateTagName_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        await SeedDietaryTag("Vegetarian");
        var request = new CreateDietaryTagRequest("Vegetarian");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
        conflict!.Value!.Code.Should().Be("CONFLICT");
        conflict.Value.Message.Should().Contain("already exists");

        // Verify no duplicate was created
        var count = await _context.DietaryTags.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_DuplicateTagNameDifferentCase_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        await SeedDietaryTag("Vegetarian");
        var request = new CreateDietaryTagRequest("VEGETARIAN");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
        conflict!.Value!.Code.Should().Be("CONFLICT");

        // Verify no duplicate was created
        var count = await _context.DietaryTags.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_DuplicateTagNameLowercase_When_Handle_Then_ReturnsConflict()
    {
        // Arrange
        await SeedDietaryTag("Vegetarian");
        var request = new CreateDietaryTagRequest("vegetarian");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var conflict = result as Conflict<ApiError>;
        conflict.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Given_SingleCharacterName_When_Handle_Then_ReturnsCreated()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("V");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DietaryTagResponse>;
        created.Should().NotBeNull();
        created!.Value!.Name.Should().Be("V");
    }

    [Fact]
    public async Task Given_LongName_When_Handle_Then_ReturnsCreated()
    {
        // Arrange
        var longName = "Suitable for people with multiple dietary restrictions including lactose intolerance";
        var request = new CreateDietaryTagRequest(longName);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DietaryTagResponse>;
        created.Should().NotBeNull();
        created!.Value!.Name.Should().Be(longName);
    }

    [Fact]
    public async Task Given_NameWithSpecialCharacters_When_Handle_Then_ReturnsCreated()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("Nut-Free (Peanut & Tree Nuts)");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<DietaryTagResponse>;
        created.Should().NotBeNull();
        created!.Value!.Name.Should().Be("Nut-Free (Peanut & Tree Nuts)");
    }

    #endregion
}

