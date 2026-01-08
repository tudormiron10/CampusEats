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

public class GetTransactionsHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetTransactionsHandler _handler = null!;

    public GetTransactionsHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetTransactionsHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetTransactionsHandler(_context);
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

    private async Task<(UserEntity User, Infrastructure.Persistence.Entities.Loyalty Loyalty)> SeedUserWithLoyalty()
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
            CurrentPoints = 100,
            LifetimePoints = 500
        };

        user.Loyalty = loyalty;

        _context.Users.Add(user);
        _context.Loyalties.Add(loyalty);
        await _context.SaveChangesAsync();

        return (user, loyalty);
    }

    private async Task<LoyaltyTransaction> SeedTransaction(
        Guid loyaltyId,
        string type,
        int points,
        string description,
        DateTime? date = null,
        Guid? orderId = null)
    {
        var transaction = new LoyaltyTransaction
        {
            TransactionId = Guid.NewGuid(),
            LoyaltyId = loyaltyId,
            Type = type,
            Points = points,
            Description = description,
            Date = date ?? DateTime.UtcNow,
            OrderId = orderId
        };

        _context.LoyaltyTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_UserWithTransactions_When_Handle_Then_ReturnsTransactionsOrderedByDateDesc()
    {
        // Arrange
        var (user, loyalty) = await SeedUserWithLoyalty();

        var older = await SeedTransaction(loyalty.LoyaltyId, "Earn", 50, "Order #1", DateTime.UtcNow.AddDays(-2));
        var newer = await SeedTransaction(loyalty.LoyaltyId, "Earn", 100, "Order #2", DateTime.UtcNow.AddDays(-1));
        var newest = await SeedTransaction(loyalty.LoyaltyId, "Redeem", -200, "Offer redeemed", DateTime.UtcNow);

        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetTransactionsRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<LoyaltyTransactionResponse>>;
        ok.Should().NotBeNull();

        var transactions = ok!.Value;
        transactions.Should().HaveCount(3);

        // Verify order is by date descending
        transactions![0].TransactionId.Should().Be(newest.TransactionId);
        transactions[1].TransactionId.Should().Be(newer.TransactionId);
        transactions[2].TransactionId.Should().Be(older.TransactionId);
    }

    [Fact]
    public async Task Given_UserWithNoTransactions_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var (user, _) = await SeedUserWithLoyalty();
        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetTransactionsRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<LoyaltyTransactionResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Transaction_When_Handle_Then_ReturnsCorrectTransactionDetails()
    {
        // Arrange
        var (user, loyalty) = await SeedUserWithLoyalty();
        var orderId = Guid.NewGuid();
        var transactionDate = DateTime.UtcNow;

        var transaction = await SeedTransaction(
            loyalty.LoyaltyId,
            type: "Earn",
            points: 150,
            description: "Earned from order",
            date: transactionDate,
            orderId: orderId
        );

        var httpContext = CreateHttpContext(user.UserId);
        var request = new GetTransactionsRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<LoyaltyTransactionResponse>>;
        ok.Should().NotBeNull();

        var response = ok!.Value!.First();
        response.TransactionId.Should().Be(transaction.TransactionId);
        response.Type.Should().Be("Earn");
        response.Points.Should().Be(150);
        response.Description.Should().Be("Earned from order");
        response.OrderId.Should().Be(orderId);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_UnauthenticatedUser_When_Handle_Then_ReturnsUnauthorized()
    {
        // Arrange
        var httpContext = CreateHttpContext(null);
        var request = new GetTransactionsRequest(httpContext);

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
        var request = new GetTransactionsRequest(httpContext);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    #endregion
}