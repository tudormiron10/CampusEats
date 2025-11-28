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

namespace CampusEats.Api.Tests.Features.Categories
{
    public class UpdateCategoryHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private UpdateCategoryHandler _handler = null!;

        public UpdateCategoryHandlerTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private UpdateCategoryHandler CreateSUT()
        {
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            return new UpdateCategoryHandler(_context);
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
        public async Task Given_ExistingCategory_When_Handle_Then_UpdatesValuesAndReturnsOk()
        {
            // Arrange
            var category = new Category
            {
                CategoryId = Guid.NewGuid(),
                Name = "Old",
                Icon = "OldIcon",
                SortOrder = 1
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var request = new UpdateCategoryRequest(category.CategoryId, "New", "NewIcon", 2);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<CategoryResponse>;
            ok.Should().NotBeNull();

            var response = ok!.Value;
            response.Should().NotBeNull();
            response.CategoryId.Should().Be(category.CategoryId);
            response.Name.Should().Be(request.Name);
            response.Icon.Should().Be(request.Icon);
            response.SortOrder.Should().Be(request.SortOrder);

            var persisted = await _context.Categories.FindAsync(category.CategoryId);
            persisted.Should().NotBeNull();
            persisted!.Name.Should().Be(request.Name);
            persisted.Icon.Should().Be(request.Icon);
            persisted.SortOrder.Should().Be(request.SortOrder);
        }

        [Fact]
        public async Task Given_NonExistentCategoryId_When_Handle_Then_ReturnsNotFoundWithMessage()
        {
            // Arrange
            var randomId = Guid.NewGuid();
            var request = new UpdateCategoryRequest(randomId, "Name", "Icon", 1);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("NotFound");

            var valueProp = result.GetType().GetProperty("Value");
            valueProp.Should().NotBeNull();
            var value = valueProp!.GetValue(result);
            value.Should().NotBeNull();

            var messageProp = value!.GetType().GetProperty("message");
            messageProp.Should().NotBeNull();
            var message = messageProp!.GetValue(value) as string;
            message.Should().Be("Category not found");
        }
    }
}

