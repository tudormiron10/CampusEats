﻿using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Validators.DietaryTags;

namespace CampusEats.Api.Tests.Validators.DietaryTags;

public class DeleteDietaryTagValidatorTests : IDisposable
{
    private DeleteDietaryTagValidator _validator;

    public DeleteDietaryTagValidatorTests()
    {
        _validator = CreateSUT();
    }

    private static DeleteDietaryTagValidator CreateSUT() => new();

    public void Dispose()
    {
        _validator = null!;
        GC.SuppressFinalize(this);
    }

    #region DietaryTagId Validation

    [Fact]
    public void Given_ValidDietaryTagId_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var request = new DeleteDietaryTagRequest(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Given_EmptyDietaryTagId_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new DeleteDietaryTagRequest(Guid.Empty);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.DietaryTagId)
            .WithErrorMessage("Dietary tag ID is required.");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Given_SpecificGuid_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var specificGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var request = new DeleteDietaryTagRequest(specificGuid);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_NewGuid_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var request = new DeleteDietaryTagRequest(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")] // Empty Guid
    public void Given_EmptyGuidString_When_Validate_Then_ShouldReturnError(string guidString)
    {
        // Arrange
        var request = new DeleteDietaryTagRequest(Guid.Parse(guidString));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.DietaryTagId);
    }

    [Theory]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    public void Given_ValidGuidStrings_When_Validate_Then_ShouldReturnValidResult(string guidString)
    {
        // Arrange
        var request = new DeleteDietaryTagRequest(Guid.Parse(guidString));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion
}

