﻿using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CampusEats.Api.Features.Categories.Handler;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusEats.Api.Tests.Features.Categories
{
    public class DeleteCategoryHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private DeleteCategoryHandler _handler = null!;

        public DeleteCategoryHandlerTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private DeleteCategoryHandler CreateSUT()
        {
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            return new DeleteCategoryHandler(_context);
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
        public async Task Given_ExistingCategory_When_Handle_Then_ReturnsNoContent_And_RemovesCategoryFromDb()
        {
            // Arrange
            var category = new Category
            {
                CategoryId = Guid.NewGuid(),
                Name = "Soup",
                Icon = "35c", // 🍜 as example
                SortOrder = 1
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var request = new DeleteCategoryRequest(category.CategoryId);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var noContent = result as Microsoft.AspNetCore.Http.HttpResults.NoContent;
            noContent.Should().NotBeNull();

            var deleted = await _context.Categories.FindAsync(category.CategoryId);
            deleted.Should().BeNull();

            var count = await _context.Categories.CountAsync();
            count.Should().Be(0);
        }

        [Fact]
        public async Task Given_NonExistentCategoryId_When_Handle_Then_ReturnsNotFound()
        {
            // Arrange
            var randomId = Guid.NewGuid();
            var request = new DeleteCategoryRequest(randomId);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert - ApiErrors.CategoryNotFound() returns NotFound<ApiError>
            var notFound = result as NotFound<ApiError>;
            notFound.Should().NotBeNull();
            notFound!.Value!.Code.Should().Be("NOT_FOUND");
            notFound.Value.Message.Should().Contain("Category");
        }
    }
}
