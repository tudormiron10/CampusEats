using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Features.Payments;
using CampusEats.Api.Features.Payments.Response;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.Payments;

public class PaymentConfirmationHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private PaymentConfirmationHandler _handler = null!;

    public PaymentConfirmationHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private PaymentConfirmationHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new PaymentConfirmationHandler(_context);
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

    private async Task<UserEntity> SeedUser()
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            Role = UserRole.Client,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128],
            Loyalty = new Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                CurrentPoints = 100,
                LifetimePoints = 500
            }
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<OrderEntity> SeedOrder(UserEntity user, decimal totalAmount = 50.00m)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = OrderStatus.Pending,
            TotalAmount = totalAmount,
            OrderDate = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    private async Task<Payment> SeedPayment(OrderEntity order, PaymentStatus status = PaymentStatus.Processing)
    {
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = order.TotalAmount,
            Status = status,
            ClientSecret = "pi_test_secret_123",
            StripePaymentIntentId = "pi_test_123",
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_ProcessingPayment_When_ConfirmAsSucceeded_Then_ReturnsOkWithUpdatedStatus()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        var payment = await SeedPayment(order, PaymentStatus.Processing);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Succeeded);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaymentResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Status.Should().Be("Succeeded");
        ok.Value.PaymentId.Should().Be(payment.PaymentId);

        // Verify persistence
        var savedPayment = await _context.Payments.FindAsync(payment.PaymentId);
        savedPayment!.Status.Should().Be(PaymentStatus.Succeeded);
        savedPayment.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_ProcessingPayment_When_ConfirmAsFailed_Then_ReturnsOkWithFailedStatus()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        var payment = await SeedPayment(order, PaymentStatus.Processing);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Failed);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaymentResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Status.Should().Be("Failed");

        // Verify persistence
        var savedPayment = await _context.Payments.FindAsync(payment.PaymentId);
        savedPayment!.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task Given_SuccessfulPayment_When_Handle_Then_AwardsLoyaltyPoints()
    {
        // Arrange
        var user = await SeedUser();
        var initialPoints = user.Loyalty!.CurrentPoints;
        var order = await SeedOrder(user, 100.00m);
        var payment = await SeedPayment(order, PaymentStatus.Processing);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Succeeded);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert - verify loyalty points were awarded
        var savedUser = await _context.Users
            .Include(u => u.Loyalty)
            .FirstAsync(u => u.UserId == user.UserId);

        savedUser.Loyalty!.CurrentPoints.Should().BeGreaterThan(initialPoints);

        // Verify transaction was created
        var transaction = await _context.LoyaltyTransactions
            .FirstOrDefaultAsync(t => t.OrderId == order.OrderId);
        transaction.Should().NotBeNull();
        transaction!.Type.Should().Be("Earned");
        transaction.Points.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Given_ProcessingPayment_When_ConfirmAsCancelled_Then_ReturnsOkWithCancelledStatus()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        var payment = await SeedPayment(order, PaymentStatus.Processing);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Cancelled);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaymentResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Status.Should().Be("Cancelled");

        // Verify persistence
        var savedPayment = await _context.Payments.FindAsync(payment.PaymentId);
        savedPayment!.Status.Should().Be(PaymentStatus.Cancelled);
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentPaymentId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentPaymentId = Guid.NewGuid();
        var request = new PaymentConfirmationRequest(nonExistentPaymentId, PaymentStatus.Succeeded);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<string>;
        notFound.Should().NotBeNull();
        notFound!.Value.Should().Contain("Payment");
    }

    [Fact]
    public async Task Given_AlreadySucceededPayment_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        var payment = await SeedPayment(order, PaymentStatus.Succeeded);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Succeeded);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("already been processed");
    }

    [Fact]
    public async Task Given_AlreadyFailedPayment_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        var payment = await SeedPayment(order, PaymentStatus.Failed);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Succeeded);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_ProcessingPayment_When_ConfirmAsProcessing_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        var payment = await SeedPayment(order, PaymentStatus.Processing);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Processing);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().Contain("Invalid status");
    }

    [Fact]
    public async Task Given_CancelledPayment_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        var payment = await SeedPayment(order, PaymentStatus.Cancelled);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Succeeded);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Given_FailedPayment_When_Handle_Then_DoesNotAwardLoyaltyPoints()
    {
        // Arrange
        var user = await SeedUser();
        var initialPoints = user.Loyalty!.CurrentPoints;
        var order = await SeedOrder(user, 100.00m);
        var payment = await SeedPayment(order, PaymentStatus.Processing);

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Failed);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert - verify loyalty points were NOT changed
        var savedUser = await _context.Users
            .Include(u => u.Loyalty)
            .FirstAsync(u => u.UserId == user.UserId);

        savedUser.Loyalty!.CurrentPoints.Should().Be(initialPoints);

        // Verify no transaction was created
        var transaction = await _context.LoyaltyTransactions
            .FirstOrDefaultAsync(t => t.OrderId == order.OrderId);
        transaction.Should().BeNull();
    }

    [Fact]
    public async Task Given_EmptyPaymentId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var request = new PaymentConfirmationRequest(Guid.Empty, PaymentStatus.Succeeded);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<string>;
        notFound.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_SuccessfulPayment_When_Handle_Then_UpdatesTimestamp()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        var payment = await SeedPayment(order, PaymentStatus.Processing);
        var beforeUpdate = DateTime.UtcNow;

        var request = new PaymentConfirmationRequest(payment.PaymentId, PaymentStatus.Succeeded);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var savedPayment = await _context.Payments.FindAsync(payment.PaymentId);
        savedPayment!.UpdatedAt.Should().NotBeNull();
        savedPayment.UpdatedAt!.Value.Should().BeOnOrAfter(beforeUpdate);
    }

    #endregion
}

