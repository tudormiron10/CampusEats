using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Loyalty.Handler;
using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;

namespace CampusEats.Api.Tests.Features.LoyaltyTests;

public class RedeemOfferHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private RedeemOfferHandler _handler = null!;

    public RedeemOfferHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private RedeemOfferHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new RedeemOfferHandler(_context);
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

    private HttpContext CreateHttpContext(Guid? userId)
    {
        var context = new DefaultHttpContext();
        if (userId.HasValue)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.Value.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }
        return context;
    }

    private async Task<(UserEntity User, Infrastructure.Persistence.Entities.Loyalty Loyalty)> SeedUserWithLoyalty(
        int currentPoints = 500,
        int lifetimePoints = 1000)
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = $"test{Guid.NewGuid():N}@example.com",
            Role = UserRole.Client,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128]
        };

        var loyalty = new Infrastructure.Persistence.Entities.Loyalty
        {
            LoyaltyId = Guid.NewGuid(),
            UserId = user.UserId,
            CurrentPoints = currentPoints,
            LifetimePoints = lifetimePoints
        };

        user.Loyalty = loyalty;

        _context.Users.Add(user);
        _context.Loyalties.Add(loyalty);
        await _context.SaveChangesAsync();

        return (user, loyalty);
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

    private async Task<Offer> SeedOffer(
        int pointCost = 100,
        bool isActive = true,
        LoyaltyTier? minimumTier = null,
        MenuItem? menuItem = null)
    {
        var offer = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = "Test Offer",
            Description = "Test description",
            PointCost = pointCost,
            IsActive = isActive,
            MinimumTier = minimumTier,
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
    public async Task Given_UserWithEnoughPoints_When_RedeemOffer_Then_DeductsPointsAndReturnsSuccess()
    {
        // Arrange
        var (user, loyalty) = await SeedUserWithLoyalty(currentPoints: 500, lifetimePoints: 1000);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(pointCost: 100, menuItem: menuItem);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert - Check status code is 200
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(200);

        // Verify points were deducted
        var updatedLoyalty = await _context.Loyalties.FindAsync(loyalty.LoyaltyId);
        updatedLoyalty!.CurrentPoints.Should().Be(400); // 500 - 100

        // Verify transaction was created
        var transaction = await _context.LoyaltyTransactions.FirstOrDefaultAsync();
        transaction.Should().NotBeNull();
        transaction!.Points.Should().Be(-100);
        transaction.Type.Should().Be("Redeemed");
    }

    [Fact]
    public async Task Given_UserExactPoints_When_RedeemOffer_Then_DeductsAllPoints()
    {
        // Arrange
        var (user, loyalty) = await SeedUserWithLoyalty(currentPoints: 200, lifetimePoints: 1000);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(pointCost: 200, menuItem: menuItem);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert - Check status code is 200
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(200);

        // Verify points are now zero
        var updatedLoyalty = await _context.Loyalties.FindAsync(loyalty.LoyaltyId);
        updatedLoyalty!.CurrentPoints.Should().Be(0);
    }

    [Fact]
    public async Task Given_GoldUser_When_RedeemGoldOffer_Then_Succeeds()
    {
        // Arrange - Gold tier requires >= 15000 lifetime points
        var (user, _) = await SeedUserWithLoyalty(currentPoints: 500, lifetimePoints: 20000);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(pointCost: 100, minimumTier: LoyaltyTier.Gold, menuItem: menuItem);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert - Check status code is 200
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(200);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_UnauthenticatedUser_When_RedeemOffer_Then_ReturnsUnauthorized()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(menuItem: menuItem);
        var httpContext = CreateHttpContext(null);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Given_UserWithoutLoyaltyRecord_When_RedeemOffer_Then_ReturnsNotFound()
    {
        // Arrange
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "No Loyalty User",
            Email = "noloyalty@example.com",
            Role = UserRole.Client,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128]
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(menuItem: menuItem);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Given_NonExistentOffer_When_RedeemOffer_Then_ReturnsNotFound()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty();
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(Guid.NewGuid(), httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Given_InactiveOffer_When_RedeemOffer_Then_ReturnsValidationError()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty();
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(isActive: false, menuItem: menuItem);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_InsufficientPoints_When_RedeemOffer_Then_ReturnsValidationError()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty(currentPoints: 50, lifetimePoints: 1000);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(pointCost: 100, menuItem: menuItem);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_BronzeUserForGoldOffer_When_RedeemOffer_Then_ReturnsValidationError()
    {
        // Arrange - Bronze tier: < 5000 lifetime points
        var (user, _) = await SeedUserWithLoyalty(currentPoints: 500, lifetimePoints: 1000);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(pointCost: 100, minimumTier: LoyaltyTier.Gold, menuItem: menuItem);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_SilverUserForGoldOffer_When_RedeemOffer_Then_ReturnsValidationError()
    {
        // Arrange - Silver tier: 5000-14999 lifetime points
        var (user, _) = await SeedUserWithLoyalty(currentPoints: 500, lifetimePoints: 8000);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(pointCost: 100, minimumTier: LoyaltyTier.Gold, menuItem: menuItem);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new RedeemOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    #endregion
}