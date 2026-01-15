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

public class GetAllOffersHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetAllOffersHandler _handler = null!;

    public GetAllOffersHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetAllOffersHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetAllOffersHandler(_context);
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

    private async Task<Offer> SeedOffer(string title, int pointCost, bool isActive = true, DateTime? createdAt = null)
    {
        var offer = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = title,
            Description = $"Description for {title}",
            PointCost = pointCost,
            IsActive = isActive,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        return offer;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ManagerUser_When_Handle_Then_ReturnsAllOffers()
    {
        // Arrange
        await SeedOffer("Active Offer", 100, isActive: true);
        await SeedOffer("Inactive Offer", 200, isActive: false);

        var httpContext = CreateHttpContext("Manager");
        var request = new GetAllOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_AdminUser_When_Handle_Then_ReturnsAllOffers()
    {
        // Arrange
        await SeedOffer("Test Offer", 100);

        var httpContext = CreateHttpContext("Admin");
        var request = new GetAllOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_MultipleOffers_When_Handle_Then_ReturnsOrderedByCreatedAtDesc()
    {
        // Arrange
        var oldest = await SeedOffer("Oldest", 100, createdAt: DateTime.UtcNow.AddDays(-2));
        var newest = await SeedOffer("Newest", 200, createdAt: DateTime.UtcNow);
        var middle = await SeedOffer("Middle", 150, createdAt: DateTime.UtcNow.AddDays(-1));

        var httpContext = CreateHttpContext("Manager");
        var request = new GetAllOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<OfferResponse>>;
        ok.Should().NotBeNull();

        var offers = ok!.Value;
        offers![0].Title.Should().Be("Newest");
        offers[1].Title.Should().Be("Middle");
        offers[2].Title.Should().Be("Oldest");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_ClientUser_When_Handle_Then_ReturnsForbid()
    {
        // Arrange
        var httpContext = CreateHttpContext("Client");
        var request = new GetAllOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Given_UnauthenticatedUser_When_Handle_Then_ReturnsForbid()
    {
        // Arrange
        var httpContext = CreateHttpContext(null);
        var request = new GetAllOffersRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidHttpResult>();
    }

    #endregion
}