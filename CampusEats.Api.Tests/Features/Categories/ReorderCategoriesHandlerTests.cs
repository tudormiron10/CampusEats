using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Categories.Handler;
using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Tests.Features.Categories;

public class ReorderCategoriesHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private ReorderCategoriesHandler _handler = null!;

    public ReorderCategoriesHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private ReorderCategoriesHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new ReorderCategoriesHandler(_context);
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

    #region Helper Methods

    private async Task<Category> SeedCategory(string name, int sortOrder = 0)
    {
        var category = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = name,
            Icon = "utensils",
            SortOrder = sortOrder
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return category;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidCategoryIds_When_Reorder_Then_ReturnsNoContent()
    {
        // Arrange
        var category1 = await SeedCategory("Category 1", 0);
        var category2 = await SeedCategory("Category 2", 1);
        var category3 = await SeedCategory("Category 3", 2);

        var request = new ReorderCategoriesRequest(new List<Guid>
        {
            category3.CategoryId,
            category1.CategoryId,
            category2.CategoryId
        });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public async Task Given_ValidCategoryIds_When_Reorder_Then_UpdatesSortOrder()
    {
        // Arrange
        var category1 = await SeedCategory("Category 1", 0);
        var category2 = await SeedCategory("Category 2", 1);
        var category3 = await SeedCategory("Category 3", 2);

        var request = new ReorderCategoriesRequest(new List<Guid>
        {
            category3.CategoryId,  // Should be 0
            category1.CategoryId,  // Should be 1
            category2.CategoryId   // Should be 2
        });

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var updatedCategory1 = await _context.Categories.FindAsync(category1.CategoryId);
        var updatedCategory2 = await _context.Categories.FindAsync(category2.CategoryId);
        var updatedCategory3 = await _context.Categories.FindAsync(category3.CategoryId);

        updatedCategory3!.SortOrder.Should().Be(0);
        updatedCategory1!.SortOrder.Should().Be(1);
        updatedCategory2!.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Given_SingleCategory_When_Reorder_Then_ReturnsNoContent()
    {
        // Arrange
        var category = await SeedCategory("Single Category", 0);

        var request = new ReorderCategoriesRequest(new List<Guid> { category.CategoryId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_NullCategoryIds_When_Reorder_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new ReorderCategoriesRequest(null!);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_EmptyCategoryIds_When_Reorder_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new ReorderCategoriesRequest(new List<Guid>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_NonExistentCategoryId_When_Reorder_Then_ReturnsBadRequest()
    {
        // Arrange
        var category = await SeedCategory("Existing Category", 0);

        var request = new ReorderCategoriesRequest(new List<Guid>
        {
            category.CategoryId,
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
    public async Task Given_AllNonExistentCategoryIds_When_Reorder_Then_ReturnsBadRequest()
    {
        // Arrange
        var request = new ReorderCategoriesRequest(new List<Guid>
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