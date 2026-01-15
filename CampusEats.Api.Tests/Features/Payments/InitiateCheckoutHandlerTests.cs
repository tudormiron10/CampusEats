using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using MediatR;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.Payments.Handler;
using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Features.Payments.Response;
using CampusEats.Api.Features.Notifications;
using CampusEats.Api.Infrastructure.Extensions;
using System.Security.Claims;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusEats.Api.Tests.Features.Payments;

public class InitiateCheckoutHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private InitiateCheckoutHandler _handler = null!;
    private IConfiguration _configuration = null!;
    private ILogger<InitiateCheckoutHandler> _logger = null!;
    private IHttpContextAccessor _httpContextAccessor = null!;
    private IPublisher _publisher = null!;

    public InitiateCheckoutHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        SetupHandler();
    }

    private void SetupHandler()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        _configuration = Substitute.For<IConfiguration>();
        _configuration["Stripe:SecretKey"].Returns("sk_test_123");
        _configuration["Stripe:PublishableKey"].Returns("pk_test_123");
        _configuration["Stripe:WebhookSecret"].Returns("whsec_test_123");

        _logger = Substitute.For<ILogger<InitiateCheckoutHandler>>();
        _publisher = Substitute.For<IPublisher>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

        _handler = new InitiateCheckoutHandler(
            _context,
            _configuration,
            _logger,
            _httpContextAccessor,
            _publisher);
    }

    private void SetupHttpContextWithUser(Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test"));
        _httpContextAccessor.HttpContext.Returns(httpContext);
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

    private async Task<UserEntity> SeedUser(bool withLoyalty = true, int loyaltyPoints = 1000)
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Name = "Test User",
            PasswordHash = new byte[] { 1, 2, 3 },
            PasswordSalt = new byte[] { 4, 5, 6 },
            Role = UserRole.Client,
            CreatedAt = DateTime.UtcNow
        };

        if (withLoyalty)
        {
            user.Loyalty = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = user.UserId,
                CurrentPoints = loyaltyPoints,
                LifetimePoints = 5000
            };
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<MenuItem> SeedMenuItem(string name = "Test Burger", decimal price = 12.99m)
    {
        var category = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = "Test Category"
        };

        var menuItem = new MenuItem
        {
            MenuItemId = Guid.NewGuid(),
            Name = name,
            Description = "Delicious",
            Price = price,
            Category = "Test Category",
            ImagePath = "test.jpg",
            IsAvailable = true
        };

        _context.Categories.Add(category);
        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        return menuItem;
    }

    private async Task<Offer> SeedOffer(int pointCost = 100, LoyaltyTier? minimumTier = null, bool isActive = true)
    {
        var offer = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = "Free Item",
            Description = "Test offer",
            PointCost = pointCost,
            MinimumTier = minimumTier ?? LoyaltyTier.Bronze,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();
        return offer;
    }

    #region Authorization Tests

    [Fact]
    public async Task Given_NoHttpContext_When_Handle_Then_ReturnsUnauthorized()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext)null!);
        var request = new InitiateCheckoutRequest(new List<CheckoutItemDto>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Given_NoUserId_When_Handle_Then_ReturnsUnauthorized()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _httpContextAccessor.HttpContext.Returns(httpContext);
        var request = new InitiateCheckoutRequest(new List<CheckoutItemDto>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Given_EmptyCart_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        SetupHttpContextWithUser(user.UserId);
        var request = new InitiateCheckoutRequest(new List<CheckoutItemDto>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("empty");
    }

    [Fact]
    public async Task Given_NonExistentMenuItem_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        SetupHttpContextWithUser(user.UserId);
        
        var nonExistentItemId = Guid.NewGuid();
        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>
            {
                new CheckoutItemDto(nonExistentItemId, 1)
            });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("not found");
    }

    #endregion

    #region Free Checkout - Failure Scenarios

    [Fact]
    public async Task Given_FreeCheckoutWithNonExistentUser_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer();
        var nonExistentUserId = Guid.NewGuid();
        SetupHttpContextWithUser(nonExistentUserId);

        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { menuItem.MenuItemId },
            new List<Guid> { offer.OfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFound<ApiError>>();
    }

    [Fact]
    public async Task Given_FreeCheckoutWithoutLoyalty_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser(withLoyalty: false);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer();
        SetupHttpContextWithUser(user.UserId);

        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { menuItem.MenuItemId },
            new List<Guid> { offer.OfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("loyalty");
    }

    [Fact]
    public async Task Given_FreeCheckoutWithInsufficientPoints_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser(loyaltyPoints: 50);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(pointCost: 100);
        SetupHttpContextWithUser(user.UserId);

        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { menuItem.MenuItemId },
            new List<Guid> { offer.OfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("Insufficient");
    }

    [Fact]
    public async Task Given_FreeCheckoutWithNonExistentOffer_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        SetupHttpContextWithUser(user.UserId);
        
        var nonExistentOfferId = Guid.NewGuid();
        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { menuItem.MenuItemId },
            new List<Guid> { nonExistentOfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("not found");
    }

    [Fact]
    public async Task Given_FreeCheckoutWithInactiveOffer_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(isActive: false);
        SetupHttpContextWithUser(user.UserId);

        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { menuItem.MenuItemId },
            new List<Guid> { offer.OfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("inactive");
    }

    [Fact]
    public async Task Given_FreeCheckoutWithUnmetTierRequirement_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser(loyaltyPoints: 1000); // User has 5000 lifetime = Silver tier
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(minimumTier: LoyaltyTier.Gold);
        SetupHttpContextWithUser(user.UserId);

        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { menuItem.MenuItemId },
            new List<Guid> { offer.OfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("tier");
    }

    [Fact]
    public async Task Given_FreeCheckoutWithNonExistentRedeemedItem_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var offer = await SeedOffer();
        SetupHttpContextWithUser(user.UserId);
        
        var nonExistentMenuItemId = Guid.NewGuid();
        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { nonExistentMenuItemId },
            new List<Guid> { offer.OfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("not found");
    }

    #endregion

    #region Free Checkout - Happy Path

    [Fact]
    public async Task Given_ValidFreeCheckout_When_Handle_Then_CreatesOrderAndDeductsPoints()
    {
        // Arrange
        var user = await SeedUser(loyaltyPoints: 500);
        var menuItem = await SeedMenuItem();
        var offer = await SeedOffer(pointCost: 100);
        SetupHttpContextWithUser(user.UserId);

        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { menuItem.MenuItemId },
            new List<Guid> { offer.OfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<FreeCheckoutResponse>;
        created.Should().NotBeNull();
        
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == user.UserId);
        
        order.Should().NotBeNull();
        order!.TotalAmount.Should().Be(0);
        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Should().HaveCount(1);
        order.Items.First().MenuItemId.Should().Be(menuItem.MenuItemId);
        order.Items.First().UnitPrice.Should().Be(0);

        var updatedUser = await _context.Users
            .Include(u => u.Loyalty)
            .FirstAsync(u => u.UserId == user.UserId);
        
        updatedUser.Loyalty!.CurrentPoints.Should().Be(400); // 500 - 100

        var transaction = await _context.LoyaltyTransactions
            .FirstOrDefaultAsync(t => t.LoyaltyId == updatedUser.Loyalty.LoyaltyId);
        
        transaction.Should().NotBeNull();
        transaction!.Type.Should().Be("Redeemed");
        transaction.Points.Should().Be(-100);

        await _publisher.Received(1).Publish(
            Arg.Is<OrderCreatedNotification>(n => n.OrderId == order.OrderId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_FreeCheckoutWithMultipleOffers_When_Handle_Then_DeductsAllPoints()
    {
        // Arrange
        var user = await SeedUser(loyaltyPoints: 500);
        var menuItem1 = await SeedMenuItem("Burger", 12.99m);
        var menuItem2 = await SeedMenuItem("Pizza", 15.99m);
        
        var offer1 = await SeedOffer(pointCost: 100);
        var offer2 = new Offer
        {
            OfferId = Guid.NewGuid(),
            Title = "Free Pizza",
            Description = "Test",
            PointCost = 150,
            MinimumTier = LoyaltyTier.Bronze,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Offers.Add(offer2);
        await _context.SaveChangesAsync();

        SetupHttpContextWithUser(user.UserId);

        var request = new InitiateCheckoutRequest(
            new List<CheckoutItemDto>(),
            new List<Guid> { menuItem1.MenuItemId, menuItem2.MenuItemId },
            new List<Guid> { offer1.OfferId, offer2.OfferId });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var created = result as Created<FreeCheckoutResponse>;
        created.Should().NotBeNull();
        
        var updatedUser = await _context.Users
            .Include(u => u.Loyalty)
            .FirstAsync(u => u.UserId == user.UserId);
        
        updatedUser.Loyalty!.CurrentPoints.Should().Be(250); // 500 - 100 - 150

        var transactions = await _context.LoyaltyTransactions
            .Where(t => t.LoyaltyId == updatedUser.Loyalty.LoyaltyId)
            .ToListAsync();
        
        transactions.Should().HaveCount(2);
        transactions.Sum(t => t.Points).Should().Be(-250);
    }

    #endregion
}
