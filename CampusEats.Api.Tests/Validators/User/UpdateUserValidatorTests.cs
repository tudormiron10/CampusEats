using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Validators.Users;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Tests.Validators.User
{
    public class UpdateUserValidatorTests : IDisposable
    {
        private UpdateUserValidator _validator;

        public UpdateUserValidatorTests()
        {
            _validator = CreateSUT();
        }

        private static UpdateUserValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var request = new UpdateUserRequest(Guid.NewGuid(), "Valid Name", "valid@email.com", UserRole.Client);

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
            var request = new UpdateUserRequest(Guid.NewGuid(), string.Empty, "valid@email.com", UserRole.Client);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Given_EmptyEmail_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UpdateUserRequest(Guid.NewGuid(), "John Doe", string.Empty, UserRole.Client);

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
            var request = new UpdateUserRequest(Guid.NewGuid(), "John Doe", "invalid-format", UserRole.Client);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Given_InvalidRole_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UpdateUserRequest(Guid.NewGuid(), "John Doe", "valid@example.com", (UserRole)99);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.Role);
        }
    }
}