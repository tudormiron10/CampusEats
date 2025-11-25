using CampusEats.Api.Features.Order.Request;
using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Validators.Orders;

namespace CampusEats.Api.Tests.Validators.Order
{
    public class CreateOrderValidatorTests : IDisposable
    {
        private CreateOrderValidator _validator;

        public CreateOrderValidatorTests()
        {
            _validator = CreateSUT();
        }

        private CreateOrderValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
        }

        [Fact]
        public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var items = new List<Guid> { Guid.NewGuid() };
            var request = new CreateOrderRequest(userId, items);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_RequestWithDuplicateItems_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var items = new List<Guid> { a, a, b };
            var request = new CreateOrderRequest(userId, items);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_EmptyUserId_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var items = new List<Guid> { Guid.NewGuid() };
            var request = new CreateOrderRequest(Guid.Empty, items);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.UserId);
            result.Errors.Should().Contain(e => e.PropertyName == "UserId" && e.ErrorMessage.IndexOf("required", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void Given_EmptyMenuItemIdsList_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new CreateOrderRequest(userId, new List<Guid>());

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.MenuItemIds);
            result.Errors.Should().Contain(e => e.PropertyName == "MenuItemIds" && e.ErrorMessage.IndexOf("at least one product", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void Given_NullMenuItemIdsList_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new CreateOrderRequest(userId, null!);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.MenuItemIds);
        }
    }
}

