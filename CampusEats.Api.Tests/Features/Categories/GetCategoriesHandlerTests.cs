using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CampusEats.Api.Features.Categories.Handler;
using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Features.Categories.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Tests.Features.Categories
{
    public class GetCategoriesHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private GetCategoriesHandler _handler = null!;

        public GetCategoriesHandlerTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private GetCategoriesHandler CreateSUT()
        {
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            return new GetCategoriesHandler(_context);
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

        [Fact]
        public async Task Given_NoCategoriesInDb_When_Handle_Then_ReturnsOkWithEmptyList()
        {
            // Arrange
            var request = new GetCategoriesRequest();

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<List<CategoryResponse>>;
            ok.Should().NotBeNull();

            var categories = ok!.Value;
            categories.Should().NotBeNull();
            categories.Should().BeEmpty();
        }

        [Fact]
        public async Task Given_CategoriesWithDifferentSortOrders_When_Handle_Then_ReturnsOrderedList()
        {
            // Arrange
            var catA = new Category
            {
                CategoryId = Guid.NewGuid(),
                Name = "B_Second",
                Icon = "IconA",
                SortOrder = 2
            };

            var catB = new Category
            {
                CategoryId = Guid.NewGuid(),
                Name = "A_First",
                Icon = "IconB",
                SortOrder = 1
            };

            var catC = new Category
            {
                CategoryId = Guid.NewGuid(),
                Name = "A_Third",
                Icon = "IconC",
                SortOrder = 2
            };

            _context.Categories.AddRange(catA, catB, catC);
            await _context.SaveChangesAsync();

            var request = new GetCategoriesRequest();

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<List<CategoryResponse>>;
            ok.Should().NotBeNull();

            var categories = ok!.Value;
            categories.Should().NotBeNull();
            categories.Should().HaveCount(3);

            // Order should be: B (Sort=1), then C (Sort=2, Name=A...), then A (Sort=2, Name=B...)
            categories[0].CategoryId.Should().Be(catB.CategoryId);
            categories[1].CategoryId.Should().Be(catC.CategoryId);
            categories[2].CategoryId.Should().Be(catA.CategoryId);
        }

        [Fact]
        public async Task Given_ExistingCategory_When_Handle_Then_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new Category
            {
                CategoryId = id,
                Name = "Mapped Name",
                Icon = "MappedIcon",
                SortOrder = 42
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var request = new GetCategoriesRequest();

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<List<CategoryResponse>>;
            ok.Should().NotBeNull();

            var categories = ok!.Value;
            categories.Should().NotBeNull();
            categories.Should().HaveCount(1);

            var response = categories.Single();
            response.CategoryId.Should().Be(category.CategoryId);
            response.Name.Should().Be(category.Name);
            response.Icon.Should().Be(category.Icon);
            response.SortOrder.Should().Be(category.SortOrder);
        }
    }
}

