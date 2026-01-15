﻿﻿using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Validators.Menu;

namespace CampusEats.Api.Tests.Validators.Menu
{
    public class UpdateMenuItemValidatorTests : IDisposable
    {
        private UpdateMenuItemValidator _validator;

        public UpdateMenuItemValidatorTests()
        {
            _validator = CreateSUT();
        }

        private static UpdateMenuItemValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new UpdateMenuItemRequest(Guid.NewGuid(), "Burger", 15.50m, "Main", null, "Desc", null, true, 1);

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
            var request = new UpdateMenuItemRequest(Guid.NewGuid(), string.Empty, 15.50m, "Main", null, "Desc", null, true, 1);

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
            var request = new UpdateMenuItemRequest(Guid.NewGuid(), longName, 15.50m, "Main", null, "Desc", null, true, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Given_ZeroPrice_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UpdateMenuItemRequest(Guid.NewGuid(), "Burger", 0m, "Main", null, "Desc", null, true, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Price);
        }

        [Fact]
        public void Given_EmptyCategory_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UpdateMenuItemRequest(Guid.NewGuid(), "Burger", 15.50m, string.Empty, null, "Desc", null, true, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Category);
        }
    }
}

