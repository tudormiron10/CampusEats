using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Loyalty.Handler;
using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Tests.Features.Loyalty;

public class UpdateOfferHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private UpdateOfferHandler _handler = null!;

    public UpdateOfferHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private UpdateOfferHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new UpdateOfferHandler(_context);
    }

    public void Dispose()
    {
        if (_context != null)
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _context = null!;
        }
    }

    #region Helper Methods

    private HttpContext CreateHttpContext(string? role)
    {
        var context = new DefaultHttpContext();
        if (role != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new(ClaimTypes.Role, role)
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }
        return context;
    }

    private async Task<MenuItem> SeedMenuItem(string name = "Test Item")
    {
        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Category = "Test",
            Description = "Test description",
            Price = 10.00m,
            IsAvailable = true,
            SortOrder = 1
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        return menuItem;
    }

    private async Task<Offer> SeedOffer(string title = "Test Offer", MenuItem? menuItem = null)
    {
        var offer = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = title,
            Description = "Original description",
            PointCost = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        if (menuItem != null)
        {
            var offerItem = new OfferItem
            {
                OfferItemId = Guid.NewGuid(),
                OfferId = offer.OfferId,
                MenuItemId = menuItem.MenuItemId,
                Quantity = 1
            };
            _context.Set<OfferItem>().Add(offerItem);
            await _context.SaveChangesAsync();
        }

        return offer;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidRequest_When_UpdateOffer_Then_UpdatesOfferFields()
    {
        // Arrange
        var menuItem = await SeedMenuItem("Burger");
        var offer = await SeedOffer("Original Title", menuItem);
        var httpContext = CreateHttpContext("Manager");
        
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "Updated Title",
            Description: "Updated Description",
            ImageUrl: "https://example.com/new-image.jpg",
            PointCost: 200,
            MinimumTier: "Silver",
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 2)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<OfferResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Title.Should().Be("Updated Title");
        ok!.Value.Description.Should().Be("Updated Description");
        ok!.Value.ImageUrl.Should().Be("https://example.com/new-image.jpg");
        ok!.Value.PointCost.Should().Be(200);
        ok!.Value.MinimumTier.Should().Be(LoyaltyTier.Silver);
    }

    [Fact]
    public async Task Given_ValidRequest_When_UpdateOffer_Then_ReplacesOfferItems()
    {
        // Arrange
        var originalItem = await SeedMenuItem("Original Item");
        var newItem = await SeedMenuItem("New Item");
        var offer = await SeedOffer("Test Offer", originalItem);
        var httpContext = CreateHttpContext("Admin");
        
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "Updated Offer",
            Description: null,
            ImageUrl: null,
            PointCost: 150,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(newItem.MenuItemId, 3)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<OfferResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Items.Should().HaveCount(1);
        ok!.Value.Items[0].MenuItemId.Should().Be(newItem.MenuItemId);
        ok!.Value.Items[0].Quantity.Should().Be(3);
        ok!.Value.Items[0].Name.Should().Be("New Item");
    }

    [Fact]
    public async Task Given_MultipleItems_When_UpdateOffer_Then_AddsAllItems()
    {
        // Arrange
        var item1 = await SeedMenuItem("Item 1");
        var item2 = await SeedMenuItem("Item 2");
        var item3 = await SeedMenuItem("Item 3");
        var offer = await SeedOffer("Multi Item Offer", item1);
        var httpContext = CreateHttpContext("Manager");
        
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "Multi Item Offer Updated",
            Description: null,
            ImageUrl: null,
            PointCost: 300,
            MinimumTier: "Gold",
            Items: new List<CreateOfferItemRequest>
            {
                new(item1.MenuItemId, 1),
                new(item2.MenuItemId, 2),
                new(item3.MenuItemId, 3)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<OfferResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Items.Should().HaveCount(3);
        ok!.Value.MinimumTier.Should().Be(LoyaltyTier.Gold);
    }

    [Fact]
    public async Task Given_NullMinimumTier_When_UpdateOffer_Then_SetsMinimumTierToNull()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(menuItem: menuItem);
        // Set initial tier
        offer.MinimumTier = LoyaltyTier.Gold;
        await _context.SaveChangesAsync();
        
        var httpContext = CreateHttpContext("Manager");
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "No Tier Offer",
            Description: null,
            ImageUrl: null,
            PointCost: 50,
            MinimumTier: null, // Clear the tier
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 1)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<OfferResponse>;
        ok.Should().NotBeNull();
        ok!.Value.MinimumTier.Should().BeNull();
    }

    [Fact]
    public async Task Given_AdminUser_When_UpdateOffer_Then_Succeeds()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(menuItem: menuItem);
        var httpContext = CreateHttpContext("Admin");
        
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "Admin Updated",
            Description: null,
            ImageUrl: null,
            PointCost: 100,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 1)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<OfferResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Title.Should().Be("Admin Updated");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_NonExistentOffer_When_UpdateOffer_Then_ReturnsNotFound()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var httpContext = CreateHttpContext("Manager");
        var request = new UpdateOfferRequest(
            OfferId: Guid.NewGuid(),
            Title: "Updated",
            Description: null,
            ImageUrl: null,
            PointCost: 100,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 1)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Given_ClientUser_When_UpdateOffer_Then_ReturnsForbid()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(menuItem: menuItem);
        var httpContext = CreateHttpContext("Client");
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "Updated",
            Description: null,
            ImageUrl: null,
            PointCost: 100,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 1)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Given_UnauthenticatedUser_When_UpdateOffer_Then_ReturnsForbid()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(menuItem: menuItem);
        var httpContext = CreateHttpContext(null);
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "Updated",
            Description: null,
            ImageUrl: null,
            PointCost: 100,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 1)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Given_InvalidMinimumTier_When_UpdateOffer_Then_ReturnsValidationError()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(menuItem: menuItem);
        var httpContext = CreateHttpContext("Manager");
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "Updated",
            Description: null,
            ImageUrl: null,
            PointCost: 100,
            MinimumTier: "InvalidTier",
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 1)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_NonExistentMenuItem_When_UpdateOffer_Then_ReturnsValidationError()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(menuItem: menuItem);
        var httpContext = CreateHttpContext("Manager");
        var request = new UpdateOfferRequest(
            OfferId: offer.OfferId,
            Title: "Updated",
            Description: null,
            ImageUrl: null,
            PointCost: 100,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(Guid.NewGuid(), 1)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    #endregion
}