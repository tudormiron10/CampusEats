﻿﻿using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Payments;

namespace CampusEats.Api.Tests.Validators.Payments
{
    public class PaymentConfirmationValidatorTests : IDisposable
    {
        private PaymentConfirmationValidator _validator;

        public PaymentConfirmationValidatorTests()
        {
            _validator = CreateSUT();
        }

        private static PaymentConfirmationValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Given_ValidSuccessStatus_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new PaymentConfirmationRequest(Guid.NewGuid(), PaymentStatus.Succeeded);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_ValidFailedStatus_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new PaymentConfirmationRequest(Guid.NewGuid(), PaymentStatus.Failed);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_EmptyPaymentId_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new PaymentConfirmationRequest(Guid.Empty, PaymentStatus.Succeeded);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.PaymentId);
        }

        [Fact]
        public void Given_InitiatedStatus_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new PaymentConfirmationRequest(Guid.NewGuid(), PaymentStatus.Pending);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.NewStatus);
            result.Errors.Should().Contain(e => e.PropertyName == "NewStatus" && e.ErrorMessage.Contains("Pending"));
        }

        [Fact]
        public void Given_InvalidEnumStatus_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new PaymentConfirmationRequest(Guid.NewGuid(), (PaymentStatus)99);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.NewStatus);
        }
    }
}

