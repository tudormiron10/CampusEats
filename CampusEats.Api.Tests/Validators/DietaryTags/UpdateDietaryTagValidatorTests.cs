﻿using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Validators.DietaryTags;

namespace CampusEats.Api.Tests.Validators.DietaryTags;

public class UpdateDietaryTagValidatorTests : IDisposable
{
    private UpdateDietaryTagValidator _validator;

    public UpdateDietaryTagValidatorTests()
    {
        _validator = CreateSUT();
    }

    private static UpdateDietaryTagValidator CreateSUT() => new();

    public void Dispose()
    {
        _validator = null!;
        GC.SuppressFinalize(this);
    }

    #region Valid Request

    [Fact]
    public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), "Vegetarian");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region DietaryTagId Validation

    [Fact]
    public void Given_EmptyDietaryTagId_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new UpdateDietaryTagRequest(Guid.Empty, "Vegetarian");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.DietaryTagId)
            .WithErrorMessage("Dietary tag ID is required.");
    }

    [Fact]
    public void Given_ValidDietaryTagId_When_Validate_Then_ShouldNotHaveError()
    {
        // Arrange
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), "Vegetarian");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.DietaryTagId);
    }

    #endregion

    #region Name Validation - Required

    [Fact]
    public void Given_EmptyName_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), "");

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
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), null!);

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
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), "   ");

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
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), "V");

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
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), "VG");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_NameExactlyMaxLength_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange - exactly 100 characters
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), new string('A', 100));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_NameTooLong_When_Validate_Then_ShouldReturnError()
    {
        // Arrange - 101 characters (exceeds max of 100)
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), new string('A', 101));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Dietary tag name must not exceed 100 characters.");
    }

    #endregion

    #region Multiple Errors

    [Fact]
    public void Given_EmptyIdAndEmptyName_When_Validate_Then_ShouldReturnMultipleErrors()
    {
        // Arrange
        var request = new UpdateDietaryTagRequest(Guid.Empty, "");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.DietaryTagId);
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Given_EmptyIdAndNameTooLong_When_Validate_Then_ShouldReturnMultipleErrors()
    {
        // Arrange
        var request = new UpdateDietaryTagRequest(Guid.Empty, new string('X', 150));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.DietaryTagId);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    #endregion

    #region Valid Name Scenarios

    [Theory]
    [InlineData("Vegetarian")]
    [InlineData("Vegan")]
    [InlineData("Gluten-Free")]
    [InlineData("Dairy-Free")]
    [InlineData("Nut-Free (All Types)")]
    public void Given_ValidDietaryTagNames_When_Validate_Then_ShouldReturnValidResult(string name)
    {
        // Arrange
        var request = new UpdateDietaryTagRequest(Guid.NewGuid(), name);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion
}

