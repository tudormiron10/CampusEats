using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Loyalty.Handler;
using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;

namespace CampusEats.Api.Tests.Features.Loyalty;

public class GetOffersHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetOffersHandler _handler = null!;

    public GetOffersHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetOffersHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetOffersHandler(_context);
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
        int lifetimePoints = 100)
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
            CurrentPoints = 500,
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
        string title,
        int pointCost,
        bool isActive = true,
        LoyaltyTier? minimumTier = null,
        MenuItem? menuItem = null)
    {
        var offer = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = title,
            Description = $"Description for {title}",
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
    public async Task Given_ActiveOffers_When_Handle_Then_ReturnsOffersOrderedByPointCost()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty();
        var menuItem = await SeedMenuItem();

        var expensive = await SeedOffer("Expensive", pointCost: 500, menuItem: menuItem);
        var cheap = await SeedOffer("Cheap", pointCost: 100, menuItem: menuItem);
        var medium = await SeedOffer("Medium", pointCost: 250, menuItem: menuItem);

        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();

        var offers = ok!.Value;
        offers.Should().HaveCount(3);
        offers![0].Title.Should().Be("Cheap");
        offers[1].Title.Should().Be("Medium");
        offers[2].Title.Should().Be("Expensive");
    }

    [Fact]
    public async Task Given_InactiveOffers_When_Handle_Then_ExcludesInactiveOffers()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty();
        var menuItem = await SeedMenuItem();

        await SeedOffer("Active Offer", pointCost: 100, isActive: true, menuItem: menuItem);
        await SeedOffer("Inactive Offer", pointCost: 50, isActive: false, menuItem: menuItem);

        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();

        var offers = ok!.Value;
        offers.Should().HaveCount(1);
        offers![0].Title.Should().Be("Active Offer");
    }

    [Fact]
    public async Task Given_BronzeUser_When_Handle_Then_OnlyReturnsBronzeAndNoTierOffers()
    {
        // Arrange
        // Bronze tier: < 5000 lifetime points
        var (user, _) = await SeedUserWithLoyalty(lifetimePoints: 1000);
        var menuItem = await SeedMenuItem();

        await SeedOffer("No Tier Required", pointCost: 100, minimumTier: null, menuItem: menuItem);
        await SeedOffer("Bronze Required", pointCost: 200, minimumTier: LoyaltyTier.Bronze, menuItem: menuItem);
        await SeedOffer("Silver Required", pointCost: 300, minimumTier: LoyaltyTier.Silver, menuItem: menuItem);
        await SeedOffer("Gold Required", pointCost: 400, minimumTier: LoyaltyTier.Gold, menuItem: menuItem);

        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();

        var offers = ok!.Value;
        offers.Should().HaveCount(2);
        offers!.Select(o => o.Title).Should().Contain("No Tier Required");
        offers.Select(o => o.Title).Should().Contain("Bronze Required");
        offers.Select(o => o.Title).Should().NotContain("Silver Required");
        offers.Select(o => o.Title).Should().NotContain("Gold Required");
    }

    [Fact]
    public async Task Given_GoldUser_When_Handle_Then_ReturnsAllTierOffers()
    {
        // Arrange
        // Gold tier: >= 15000 lifetime points
        var (user, _) = await SeedUserWithLoyalty(lifetimePoints: 20000);
        var menuItem = await SeedMenuItem();

        await SeedOffer("No Tier Required", pointCost: 100, minimumTier: null, menuItem: menuItem);
        await SeedOffer("Bronze Required", pointCost: 200, minimumTier: LoyaltyTier.Bronze, menuItem: menuItem);
        await SeedOffer("Silver Required", pointCost: 300, minimumTier: LoyaltyTier.Silver, menuItem: menuItem);
        await SeedOffer("Gold Required", pointCost: 400, minimumTier: LoyaltyTier.Gold, menuItem: menuItem);

        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();

        var offers = ok!.Value;
        offers.Should().HaveCount(4);
    }

    [Fact]
    public async Task Given_NoOffers_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty();
        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_UnauthenticatedUser_When_Handle_Then_ReturnsUnauthorized()
    {
        // Arrange
        var httpContext = CreateHttpContext(null);
        var request = new GetOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Given_UserWithoutLoyaltyRecord_When_Handle_Then_AutoCreatesLoyaltyAndReturnsOffers()
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

        // Seed an active offer without tier restriction
        await SeedOffer("Free Offer", 100, isActive: true);

        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert - Handler now auto-creates loyalty record and returns available offers
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();

        var offers = ok!.Value;
        offers.Should().NotBeNull();
        offers.Should().HaveCount(1);

        // Verify loyalty record was created in database
        var createdLoyalty = await _context.Loyalties.FirstOrDefaultAsync(l => l.UserId == user.UserId);
        createdLoyalty.Should().NotBeNull();
        createdLoyalty!.CurrentPoints.Should().Be(0);
        createdLoyalty.LifetimePoints.Should().Be(0);
    }

    #endregion
}