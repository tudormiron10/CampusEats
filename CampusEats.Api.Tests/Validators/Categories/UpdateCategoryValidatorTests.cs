﻿using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Validators.Category;

namespace CampusEats.Api.Tests.Validators.Categories
{
    public class UpdateCategoryValidatorTests : IDisposable
    {
        private UpdateCategoryValidator _validator;

        public UpdateCategoryValidatorTests()
        {
            _validator = CreateSUT();
        }

        private UpdateCategoryValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new UpdateCategoryRequest(Guid.NewGuid(), "Soups", "🍜", 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_EmptyCategoryId_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UpdateCategoryRequest(Guid.Empty, "Soups", "🍜", 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.CategoryId);
        }

        [Fact]
        public void Given_EmptyName_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UpdateCategoryRequest(Guid.NewGuid(), string.Empty, "🍜", 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Given_NameExceedsMaxLength_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var longName = new string('a', 101);
            var request = new UpdateCategoryRequest(Guid.NewGuid(), longName, "🍜", 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Given_EmptyIcon_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UpdateCategoryRequest(Guid.NewGuid(), "Soups", string.Empty, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Icon);
        }

        [Fact]
        public void Given_NegativeSortOrder_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UpdateCategoryRequest(Guid.NewGuid(), "Soups", "🍜", -5);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.SortOrder);
        }
    }
}

