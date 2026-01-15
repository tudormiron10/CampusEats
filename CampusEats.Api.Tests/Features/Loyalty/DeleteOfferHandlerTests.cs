using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Loyalty.Handler;
using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Tests.Features.Loyalty;

public class DeleteOfferHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private DeleteOfferHandler _handler = null!;

    public DeleteOfferHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private DeleteOfferHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new DeleteOfferHandler(_context);
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

    private async Task<Offer> SeedOffer(string title = "Test Offer")
    {
        var offer = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = title,
            Description = "Test description",
            PointCost = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        return offer;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ExistingOffer_When_Handle_Then_DeletesOfferAndReturnsNoContent()
    {
        // Arrange
        var offer = await SeedOffer();
        var httpContext = CreateHttpContext("Manager");
        var request = new DeleteOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        var deletedOffer = await _context.Offers.FindAsync(offer.OfferId);
        deletedOffer.Should().BeNull();
    }

    [Fact]
    public async Task Given_AdminUser_When_Handle_Then_DeletesOffer()
    {
        // Arrange
        var offer = await SeedOffer();
        var httpContext = CreateHttpContext("Admin");
        var request = new DeleteOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_NonExistentOffer_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var httpContext = CreateHttpContext("Manager");
        var request = new DeleteOfferRequest(Guid.NewGuid(), httpContext);

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
        var request = new DeleteOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidHttpResult>();

        // Verify offer was not deleted
        var existingOffer = await _context.Offers.FindAsync(offer.OfferId);
        existingOffer.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_UnauthenticatedUser_When_Handle_Then_ReturnsForbid()
    {
        // Arrange
        var offer = await SeedOffer();
        var httpContext = CreateHttpContext(null);
        var request = new DeleteOfferRequest(offer.OfferId, httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidHttpResult>();
    }

    #endregion
}