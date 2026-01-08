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

namespace CampusEats.Api.Tests.Features.LoyaltyTests;

public class GetLoyaltyStatusHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetLoyaltyStatusHandler _handler = null!;

    public GetLoyaltyStatusHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetLoyaltyStatusHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetLoyaltyStatusHandler(_context);
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
        int currentPoints = 100,
        int lifetimePoints = 100)
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
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

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_UserWithBronzeTier_When_Handle_Then_ReturnsCorrectStatus()
    {
        // Arrange
        var (user, loyalty) = await SeedUserWithLoyalty(currentPoints: 500, lifetimePoints: 1000);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetLoyaltyStatusRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<LoyaltyStatusResponse>;
        ok.Should().NotBeNull();

        var response = ok!.Value;
        response.Should().NotBeNull();
        response!.CurrentPoints.Should().Be(500);
        response.LifetimePoints.Should().Be(1000);
        response.Tier.Should().Be(LoyaltyTier.Bronze);
        response.PointsToNextTier.Should().Be(4000); // 5000 - 1000
        response.NextTierThreshold.Should().Be(5000);
    }

    [Fact]
    public async Task Given_UserWithSilverTier_When_Handle_Then_ReturnsCorrectStatus()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty(currentPoints: 2000, lifetimePoints: 7500);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetLoyaltyStatusRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<LoyaltyStatusResponse>;
        ok.Should().NotBeNull();

        var response = ok!.Value;
        response!.Tier.Should().Be(LoyaltyTier.Silver);
        response.PointsToNextTier.Should().Be(7500); // 15000 - 7500
        response.NextTierThreshold.Should().Be(15000);
    }

    [Fact]
    public async Task Given_UserWithGoldTier_When_Handle_Then_ReturnsZeroPointsToNextTier()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty(currentPoints: 5000, lifetimePoints: 20000);
        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetLoyaltyStatusRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<LoyaltyStatusResponse>;
        ok.Should().NotBeNull();

        var response = ok!.Value;
        response!.Tier.Should().Be(LoyaltyTier.Gold);
        response.PointsToNextTier.Should().Be(0); // Gold is max tier
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_UnauthenticatedUser_When_Handle_Then_ReturnsUnauthorized()
    {
        // Arrange
        var httpContext = CreateHttpContext(null);
        var request = new GetLoyaltyStatusRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Given_UserWithoutLoyaltyRecord_When_Handle_Then_ReturnsNotFound()
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

        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetLoyaltyStatusRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Given_NonExistentUserId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var httpContext = CreateHttpContext(nonExistentUserId);
        var request = new GetLoyaltyStatusRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    #endregion
}