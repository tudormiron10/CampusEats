using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Payments;

namespace CampusEats.Api.Tests.Validators.Payments;

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

    #region OrderId Validation

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
    public void Given_EmptyOrderId_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new CreatePaymentRequest(Guid.Empty);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.OrderId)
            .WithErrorMessage("Order ID is required to initiate a payment.");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Given_ValidGuid_When_Validate_Then_ShouldPass()
    {
        // Arrange
        var orderId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var request = new CreatePaymentRequest(orderId);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_NewGuid_When_Validate_Then_ShouldPass()
    {
        // Arrange
        var request = new CreatePaymentRequest(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion
}

