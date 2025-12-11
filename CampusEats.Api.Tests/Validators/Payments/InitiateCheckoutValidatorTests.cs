using FluentAssertions;
using FluentValidation.TestHelper;
using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Validators.Payments;

namespace CampusEats.Api.Tests.Validators.Payments;

public class InitiateCheckoutValidatorTests : IDisposable
{
    private InitiateCheckoutValidator _validator;

    public InitiateCheckoutValidatorTests()
    {
        _validator = CreateSUT();
    }

    private InitiateCheckoutValidator CreateSUT() => new();

    public void Dispose()
    {
        _validator = null!;
    }

    #region Items Validation

    [Fact]
    public void Given_ValidRequest_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 2),
            new CheckoutItemDto(Guid.NewGuid(), 1)
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Given_NullItems_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new InitiateCheckoutRequest(null!);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void Given_EmptyItems_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var request = new InitiateCheckoutRequest(new List<CheckoutItemDto>());

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Items)
            .WithErrorMessage("Cart cannot be empty.");
    }

    #endregion

    #region Item MenuItemId Validation

    [Fact]
    public void Given_ItemWithEmptyMenuItemId_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.Empty, 1)
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.ErrorMessage.Contains("Menu item ID is required"));
    }

    [Fact]
    public void Given_MultipleItemsWithOneEmptyMenuItemId_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 1), // Valid
            new CheckoutItemDto(Guid.Empty, 2)       // Invalid
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.ErrorMessage.Contains("Menu item ID is required"));
    }

    #endregion

    #region Item Quantity Validation

    [Fact]
    public void Given_ItemWithZeroQuantity_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 0)
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.ErrorMessage.Contains("Quantity must be at least 1"));
    }

    [Fact]
    public void Given_ItemWithNegativeQuantity_When_Validate_Then_ShouldReturnError()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), -5)
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.ErrorMessage.Contains("Quantity must be at least 1"));
    }

    [Fact]
    public void Given_ItemWithValidQuantity_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 10)
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Optional Fields Validation

    [Fact]
    public void Given_RequestWithRedeemedItems_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 1)
        };
        var redeemedItems = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var request = new InitiateCheckoutRequest(items, redeemedItems);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_RequestWithPendingOffers_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 1)
        };
        var pendingOffers = new List<Guid> { Guid.NewGuid() };
        var request = new InitiateCheckoutRequest(items, null, pendingOffers);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_RequestWithAllOptionalFields_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 2)
        };
        var redeemedItems = new List<Guid> { Guid.NewGuid() };
        var pendingOffers = new List<Guid> { Guid.NewGuid() };
        var request = new InitiateCheckoutRequest(items, redeemedItems, pendingOffers);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Given_SingleItemWithQuantityOne_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 1)
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_LargeQuantity_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.NewGuid(), 1000)
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_ManyItems_When_Validate_Then_ShouldReturnValidResult()
    {
        // Arrange
        var items = Enumerable.Range(1, 50)
            .Select(_ => new CheckoutItemDto(Guid.NewGuid(), 1))
            .ToList();
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Given_MultipleInvalidItems_When_Validate_Then_ShouldReturnMultipleErrors()
    {
        // Arrange
        var items = new List<CheckoutItemDto>
        {
            new CheckoutItemDto(Guid.Empty, 0),  // Both invalid
            new CheckoutItemDto(Guid.Empty, -1)  // Both invalid
        };
        var request = new InitiateCheckoutRequest(items);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(2);
    }

    #endregion
}

