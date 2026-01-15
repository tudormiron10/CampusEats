﻿using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Validators.Category;

namespace CampusEats.Api.Tests.Validators.Categories
{
    public class CreateCategoryValidatorTests : IDisposable
    {
        private CreateCategoryValidator _validator;

        public CreateCategoryValidatorTests()
        {
            _validator = CreateSUT();
        }

        private CreateCategoryValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new CreateCategoryRequest("Soups", "🍜", 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_EmptyName_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateCategoryRequest(string.Empty, "🍜", 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Name);
            result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Given_NameExceedsMaxLength_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var longName = new string('a', 101);
            var request = new CreateCategoryRequest(longName, "🍜", 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Name);
            result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("100 characters", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Given_EmptyIcon_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateCategoryRequest("Soups", string.Empty, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Icon);
            result.Errors.Should().Contain(e => e.PropertyName == "Icon" && e.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Given_IconExceedsMaxLength_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var longIcon = new string('i', 11);
            var request = new CreateCategoryRequest("Soups", longIcon, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Icon);
            result.Errors.Should().Contain(e => e.PropertyName == "Icon" && e.ErrorMessage.Contains("10 characters", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Given_NegativeSortOrder_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateCategoryRequest("Soups", "🍜", -1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.SortOrder);
            result.Errors.Should().Contain(e => e.PropertyName == "SortOrder" && e.ErrorMessage.Contains("0 or greater", StringComparison.OrdinalIgnoreCase));
        }
        
        [Fact]
        public void Given_NameIsExactlyMaxLength_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var longName = new string('a', 100); 
            var request = new CreateCategoryRequest(longName, "🍔", 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Given_IconIsExactlyMaxLength_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var longIcon = new string('x', 10);
            var request = new CreateCategoryRequest("Burger", longIcon, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Given_SortOrderIsZero_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new CreateCategoryRequest("Burger", "🍔", 0);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
        }
    }
}

