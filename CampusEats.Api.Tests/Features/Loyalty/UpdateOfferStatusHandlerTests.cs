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

namespace CampusEats.Api.Tests.Features.Loyalty;

public class UpdateOfferStatusHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private UpdateOfferStatusHandler _handler = null!;

    public UpdateOfferStatusHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private UpdateOfferStatusHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new UpdateOfferStatusHandler(_context);
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

    private async Task<Offer> SeedOffer(bool isActive = true)
    {
        var offer = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = "Test Offer",
            Description = "Test description",
            PointCost = 100,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        return offer;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ActiveOffer_When_SetToInactive_Then_UpdatesStatusAndReturnsOffer()
    {
        // Arrange
        var offer = await SeedOffer(isActive: true);
        var httpContext = CreateHttpContext("Manager");
        var request = new UpdateOfferStatusRequest(offer.OfferId, false, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<OfferResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.IsActive.Should().BeFalse();

        // Verify in database
        var updatedOffer = await _context.Offers.FindAsync(offer.OfferId);
        updatedOffer!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Given_InactiveOffer_When_SetToActive_Then_UpdatesStatusAndReturnsOffer()
    {
        // Arrange
        var offer = await SeedOffer(isActive: false);
        var httpContext = CreateHttpContext("Manager");
        var request = new UpdateOfferStatusRequest(offer.OfferId, true, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<OfferResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.IsActive.Should().BeTrue();

        // Verify in database
        var updatedOffer = await _context.Offers.FindAsync(offer.OfferId);
        updatedOffer!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Given_AdminUser_When_Handle_Then_UpdatesStatus()
    {
        // Arrange
        var offer = await SeedOffer(isActive: true);
        var httpContext = CreateHttpContext("Admin");
        var request = new UpdateOfferStatusRequest(offer.OfferId, false, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<OfferResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.IsActive.Should().BeFalse();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_NonExistentOffer_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var httpContext = CreateHttpContext("Manager");
        var request = new UpdateOfferStatusRequest(Guid.NewGuid(), true, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Given_ClientUser_When_Handle_Then_ReturnsForbid()
    {
        // Arrange
        var offer = await SeedOffer();
        var httpContext = CreateHttpContext("Client");
        var request = new UpdateOfferStatusRequest(offer.OfferId, false, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Given_UnauthenticatedUser_When_Handle_Then_ReturnsForbid()
    {
        // Arrange
        var offer = await SeedOffer();
        var httpContext = CreateHttpContext(null);
        var request = new UpdateOfferStatusRequest(offer.OfferId, false, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidHttpResult>();
    }

    #endregion
}