using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Validators.Payments;

namespace CampusEats.Api.Tests.Validators.Payments
{
    public class CreatePaymentValidatorTests : IDisposable
    {
        private CreatePaymentValidator _validator;

        public CreatePaymentValidatorTests()
        {
            _validator = CreateSUT();
        }

        private CreatePaymentValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
        }

        [Fact]
        public void Given_ValidOrderId_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new CreatePaymentRequest(Guid.NewGuid());

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_EmptyOrderId_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreatePaymentRequest(Guid.Empty);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.OrderId);
            result.Errors.Should().Contain(e => e.PropertyName == "OrderId" && e.ErrorMessage.IndexOf("required", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}

