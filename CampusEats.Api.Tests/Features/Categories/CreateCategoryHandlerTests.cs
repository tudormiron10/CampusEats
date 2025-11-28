using System;
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
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Tests.Features.Categories
{
    public class CreateCategoryHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private CreateCategoryHandler _handler = null!;

        public CreateCategoryHandlerTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private CreateCategoryHandler CreateSUT()
        {
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            return new CreateCategoryHandler(_context);
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
        public async Task Given_ValidRequest_When_Handle_Then_ReturnsCreatedAndPersistsCategory()
        {
            // Arrange
            var request = new CreateCategoryRequest("Pizza", "🍕", 1);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert result type
            var createdResult = result as Microsoft.AspNetCore.Http.HttpResults.Created<CategoryResponse>;
            createdResult.Should().NotBeNull();

            var response = createdResult!.Value;
            response.Should().NotBeNull();
            response.Name.Should().Be(request.Name);
            response.Icon.Should().Be(request.Icon);
            response.SortOrder.Should().Be(request.SortOrder);

            // Assert persistence
            var category = await _context.Categories.SingleAsync(c => c.CategoryId == response.CategoryId);
            category.Name.Should().Be(request.Name);
            category.Icon.Should().Be(request.Icon);
            category.SortOrder.Should().Be(request.SortOrder);
        }

        [Fact]
        public async Task Given_RequestWithSpecificSortOrderAndIcon_When_Handle_Then_MapsPropertiesCorrectly()
        {
            // Arrange
            var request = new CreateCategoryRequest("Burgers", "🍔", 100);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var createdResult = result as Microsoft.AspNetCore.Http.HttpResults.Created<CategoryResponse>;
            createdResult.Should().NotBeNull();

            var response = createdResult!.Value;
            response.Should().NotBeNull();

            var category = await _context.Categories.SingleAsync(c => c.CategoryId == response.CategoryId);
            category.SortOrder.Should().Be(100);
            category.Icon.Should().Be("🍔");
        }

        [Fact]
        public async Task Given_MultipleRequests_When_Handle_Then_AllCategoriesArePersisted()
        {
            // Arrange
            var request1 = new CreateCategoryRequest("Drinks", "🥤", 1);
            var request2 = new CreateCategoryRequest("Desserts", "🍰", 2);

            // Act
            var result1 = await _handler.Handle(request1, CancellationToken.None);
            var result2 = await _handler.Handle(request2, CancellationToken.None);

            // Basic sanity check on results
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();

            var count = await _context.Categories.CountAsync();
            count.Should().Be(2);
        }
    }
}

