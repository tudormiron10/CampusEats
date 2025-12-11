using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Validators.DietaryTags;

namespace CampusEats.Api.Tests.Validators.DietaryTags;

public class CreateDietaryTagValidatorTests : IDisposable
{
    private CreateDietaryTagValidator _validator;

    public CreateDietaryTagValidatorTests()
    {
        _validator = CreateSUT();
    }

    private CreateDietaryTagValidator CreateSUT() => new();

    public void Dispose()
    {
        _validator = null!;
    }

    #region Name Validation - Required

    [Fact]
    public void Given_ValidName_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("Vegetarian");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Given_EmptyName_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Dietary tag name is required.");
    }

    [Fact]
    public void Given_NullName_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new CreateDietaryTagRequest(null!);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Given_WhitespaceOnlyName_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("   ");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    #endregion

    #region Name Validation - Length

    [Fact]
    public void Given_NameTooShort_When_Validate_Then_ShouldReturnError()
    {
        // Arrange - single character is too short (min 2)
        var request = new CreateDietaryTagRequest("V");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Dietary tag name must be at least 2 characters.");
    }

    [Fact]
    public void Given_NameExactlyMinLength_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange - exactly 2 characters
        var request = new CreateDietaryTagRequest("VG");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_NameExactlyMaxLength_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange - exactly 100 characters
        var request = new CreateDietaryTagRequest(new string('A', 100));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_NameTooLong_When_Validate_Then_ShouldReturnError()
    {
        // Arrange - 101 characters (exceeds max of 100)
        var request = new CreateDietaryTagRequest(new string('A', 101));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Dietary tag name must not exceed 100 characters.");
    }

    #endregion

    #region Valid Name Scenarios

    [Theory]
    [InlineData("Vegetarian")]
    [InlineData("Vegan")]
    [InlineData("Gluten-Free")]
    [InlineData("Dairy-Free")]
    [InlineData("Nut-Free (All Types)")]
    [InlineData("100% Organic")]
    [InlineData("Low-Fat & Low-Sodium")]
    public void Given_ValidDietaryTagNames_When_Validate_Then_ShouldReturnValidResult(string name)
    {
        // Arrange
        var request = new CreateDietaryTagRequest(name);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_NameWithSpecialCharacters_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("Café Style (Végétarien)");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_NameWithNumbers_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var request = new CreateDietaryTagRequest("100% Natural");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion
}

