using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Validators.Menu;

namespace CampusEats.Api.Tests.Validators.Menu
{
    public class CreateMenuItemValidatorTests : IDisposable
    {
        private CreateMenuItemValidator _validator;

        public CreateMenuItemValidatorTests()
        {
            _validator = CreateSUT();
        }

        private CreateMenuItemValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
        }

        [Fact]
        public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new CreateMenuItemRequest("Burger", 15.50m, "Main", "Delicious", null, null, true, 1);

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
            var request = new CreateMenuItemRequest(string.Empty, 15.50m, "Main", "Delicious", null, null, true, 1);

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
            var request = new CreateMenuItemRequest(longName, 15.50m, "Main", "Delicious", null, null, true, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Name);
            result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("100 characters", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Given_ZeroPrice_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateMenuItemRequest("Burger", 0m, "Main", "Delicious", null, null, true, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Price);
        }

        [Fact]
        public void Given_NegativePrice_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateMenuItemRequest("Burger", -10m, "Main", "Delicious", null, null, true, 1);

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
            var request = new CreateMenuItemRequest("Burger", 15.50m, string.Empty, "Delicious", null, null, true, 1);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Category);
        }
    }
}

