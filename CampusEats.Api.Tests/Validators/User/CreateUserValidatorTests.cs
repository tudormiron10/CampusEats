﻿using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Validators.Users;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Tests.Validators.User
{
    public class CreateUserValidatorTests : IDisposable
    {
        private CreateUserValidator _validator;

        public CreateUserValidatorTests()
        {
            _validator = CreateSUT();
        }

        private CreateUserValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new CreateUserRequest("Test Name", "test@example.com", UserRole.Client, "Password123");

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
            var request = new CreateUserRequest("", "test@example.com", UserRole.Client, "Password123");

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Given_InvalidEmail_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateUserRequest("John Doe", "invalid-email", UserRole.Client, "Password123");

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Given_EmptyEmail_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateUserRequest("John Doe", "", UserRole.Client, "Password123");

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
            var request = new CreateUserRequest("John Doe", "test@example.com", UserRole.Client, "");

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Password);
        }

        [Fact]
        public void Given_ShortPassword_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateUserRequest("John Doe", "test@example.com", UserRole.Client, "123");

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Password);
        }

        [Fact]
        public void Given_InvalidRole_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new CreateUserRequest("John Doe", "test@example.com", (UserRole)99, "Password123");

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Role);
        }
    }
}