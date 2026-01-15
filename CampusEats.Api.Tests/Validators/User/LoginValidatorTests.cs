﻿using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Validators.User;
using CampusEats.Api.Features.User.Request;

namespace CampusEats.Api.Tests.Validators.User
{
    public class LoginValidatorTests : IDisposable
    {
        private LoginValidator _validator;

        public LoginValidatorTests()
        {
            _validator = CreateSUT();
        }

        private LoginValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Given_ValidCredentials_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new LoginRequest("john@example.com", "StrongPassword123!");

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_EmptyEmail_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new LoginRequest(string.Empty, "StrongPassword123!");

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Given_InvalidEmailFormat_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new LoginRequest("not-an-email", "StrongPassword123!");

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Given_EmptyPassword_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new LoginRequest("john@example.com", string.Empty);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Password);
        }
    }
}
