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

public class CreateOfferHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private CreateOfferHandler _handler = null!;

    public CreateOfferHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private CreateOfferHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new CreateOfferHandler(_context);
    }

    public void Dispose()
    {
        if (_context != null)
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _context = null!;
        }
        GC.SuppressFinalize(this);
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

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ManagerUser_When_CreateOffer_Then_ReturnsCreatedWithOffer()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var httpContext = CreateHttpContext("Manager");
        var request = new CreateOfferRequest(
            Title: "Test Offer",
            Description: "Test description",
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
        var created = result as Created<OfferResponse>;
        created.Should().NotBeNull();
        created!.Value.Should().NotBeNull();
        created.Value!.Title.Should().Be("Test Offer");
        created.Value.PointCost.Should().Be(100);
        created.Value.IsActive.Should().BeTrue();
        created.Value.Items.Should().HaveCount(1);

        // Verify in database
        var savedOffer = await _context.Offers.FirstOrDefaultAsync();
        savedOffer.Should().NotBeNull();
        savedOffer!.Title.Should().Be("Test Offer");
    }

    [Fact]
    public async Task Given_AdminUser_When_CreateOffer_Then_ReturnsCreated()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var httpContext = CreateHttpContext("Admin");
        var request = new CreateOfferRequest(
            Title: "Admin Offer",
            Description: "Created by admin",
            ImageUrl: "https://example.com/image.jpg",
            PointCost: 200,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 2)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<OfferResponse>;
        created.Should().NotBeNull();
        created!.Value!.Title.Should().Be("Admin Offer");
    }

    [Fact]
    public async Task Given_ValidMinimumTier_When_CreateOffer_Then_SetsMinimumTier()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var httpContext = CreateHttpContext("Manager");
        var request = new CreateOfferRequest(
            Title: "Gold Offer",
            Description: "Exclusive for Gold members",
            ImageUrl: null,
            PointCost: 500,
            MinimumTier: "Gold",
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem.MenuItemId, 1)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<OfferResponse>;
        created.Should().NotBeNull();
        created!.Value!.MinimumTier.Should().Be(LoyaltyTier.Gold);

        // Verify in database
        var savedOffer = await _context.Offers.FirstOrDefaultAsync();
        savedOffer!.MinimumTier.Should().Be(LoyaltyTier.Gold);
    }

    [Fact]
    public async Task Given_MultipleMenuItems_When_CreateOffer_Then_CreatesAllOfferItems()
    {
        // Arrange
        var menuItem1 = await SeedMenuItem("Item 1");
        var menuItem2 = await SeedMenuItem("Item 2");
        var httpContext = CreateHttpContext("Manager");
        var request = new CreateOfferRequest(
            Title: "Combo Offer",
            Description: "Multiple items",
            ImageUrl: null,
            PointCost: 300,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(menuItem1.MenuItemId, 1),
                new(menuItem2.MenuItemId, 2)
            },
            HttpContext: httpContext
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<OfferResponse>;
        created.Should().NotBeNull();
        created!.Value!.Items.Should().HaveCount(2);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_ClientUser_When_CreateOffer_Then_ReturnsForbid()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var httpContext = CreateHttpContext("Client");
        var request = new CreateOfferRequest(
            Title: "Test Offer",
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

        // Verify nothing was created
        var offers = await _context.Offers.ToListAsync();
        offers.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_UnauthenticatedUser_When_CreateOffer_Then_ReturnsForbid()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var httpContext = CreateHttpContext(null);
        var request = new CreateOfferRequest(
            Title: "Test Offer",
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
    public async Task Given_InvalidMinimumTier_When_CreateOffer_Then_ReturnsValidationError()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var httpContext = CreateHttpContext("Manager");
        var request = new CreateOfferRequest(
            Title: "Test Offer",
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
    public async Task Given_NonExistentMenuItem_When_CreateOffer_Then_ReturnsValidationError()
    {
        // Arrange
        var httpContext = CreateHttpContext("Manager");
        var nonExistentId = Guid.NewGuid();
        var request = new CreateOfferRequest(
            Title: "Test Offer",
            Description: null,
            ImageUrl: null,
            PointCost: 100,
            MinimumTier: null,
            Items: new List<CreateOfferItemRequest>
            {
                new(nonExistentId, 1)
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