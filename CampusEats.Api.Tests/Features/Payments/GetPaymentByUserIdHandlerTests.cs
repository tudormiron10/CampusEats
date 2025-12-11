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

public class GetPaymentByUserIdHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private GetPaymentByUserIdHandler _handler = null!;

    public GetPaymentByUserIdHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private GetPaymentByUserIdHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetPaymentByUserIdHandler(_context);
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

    private async Task<UserEntity> SeedUser(string name = "Test User", string email = "test@example.com")
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = name,
            Email = email,
            Role = UserRole.Client,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128]
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

    private async Task<Payment> SeedPayment(OrderEntity order, PaymentStatus status = PaymentStatus.Succeeded)
    {
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = order.TotalAmount,
            Status = status,
            ClientSecret = "pi_test_secret_" + Guid.NewGuid().ToString()[..8],
            StripePaymentIntentId = "pi_test_" + Guid.NewGuid().ToString()[..8],
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_UserWithPayments_When_Handle_Then_ReturnsOkWithPaymentsList()
    {
        // Arrange
        var user = await SeedUser();
        var order1 = await SeedOrder(user, 25.00m);
        var order2 = await SeedOrder(user, 75.00m);
        var payment1 = await SeedPayment(order1);
        var payment2 = await SeedPayment(order2);

        var request = new GetPaymentByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<PaymentResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(2);
        ok.Value!.Should().Contain(p => p.PaymentId == payment1.PaymentId);
        ok.Value.Should().Contain(p => p.PaymentId == payment2.PaymentId);
    }

    [Fact]
    public async Task Given_UserWithPayments_When_Handle_Then_ReturnsMappedPaymentResponses()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, 99.99m);
        var payment = await SeedPayment(order, PaymentStatus.Succeeded);

        var request = new GetPaymentByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<PaymentResponse>>;
        ok.Should().NotBeNull();
        
        var responsePayment = ok!.Value!.First();
        responsePayment.PaymentId.Should().Be((Guid)payment.PaymentId);
        responsePayment.OrderId.Should().Be((Guid)order.OrderId);
        responsePayment.Amount.Should().Be(99.99m);
        responsePayment.Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task Given_UserWithMultiplePaymentStatuses_When_Handle_Then_ReturnsAllStatuses()
    {
        // Arrange
        var user = await SeedUser();
        
        var order1 = await SeedOrder(user, 10.00m);
        var order2 = await SeedOrder(user, 20.00m);
        var order3 = await SeedOrder(user, 30.00m);
        
        await SeedPayment(order1, PaymentStatus.Succeeded);
        await SeedPayment(order2, PaymentStatus.Failed);
        await SeedPayment(order3, PaymentStatus.Processing);

        var request = new GetPaymentByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<PaymentResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(3);
        ok.Value!.Select(p => p.Status).Should().Contain(new[] { "Succeeded", "Failed", "Processing" });
    }

    [Fact]
    public async Task Given_UserWithNoPayments_When_Handle_Then_ReturnsEmptyList()
    {
        // Arrange
        var user = await SeedUser();
        var request = new GetPaymentByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<PaymentResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MultipleUsers_When_Handle_Then_ReturnsOnlyRequestedUserPayments()
    {
        // Arrange
        var user1 = await SeedUser("Alice", "alice@example.com");
        var user2 = await SeedUser("Bob", "bob@example.com");

        var order1 = await SeedOrder(user1, 50.00m);
        var order2 = await SeedOrder(user2, 100.00m);

        var payment1 = await SeedPayment(order1);
        await SeedPayment(order2);

        var request = new GetPaymentByUserIdRequest(user1.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<PaymentResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);
        ok.Value!.First().PaymentId.Should().Be(payment1.PaymentId);
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task Given_NonExistentUserId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var request = new GetPaymentByUserIdRequest(nonExistentUserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<string>;
        notFound.Should().NotBeNull();
        notFound!.Value.Should().Contain("User");
    }

    [Fact]
    public async Task Given_EmptyUserId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var request = new GetPaymentByUserIdRequest(Guid.Empty);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<string>;
        notFound.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Given_ManyPayments_When_Handle_Then_ReturnsAllPayments()
    {
        // Arrange
        var user = await SeedUser();
        const int paymentCount = 20;

        for (int i = 0; i < paymentCount; i++)
        {
            var order = await SeedOrder(user, 10.00m * (i + 1));
            await SeedPayment(order);
        }

        var request = new GetPaymentByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<PaymentResponse>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(paymentCount);
    }

    [Fact]
    public async Task Given_PaymentWithNullClientSecret_When_Handle_Then_ReturnsNullInResponse()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user);
        
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = order.TotalAmount,
            Status = PaymentStatus.Succeeded,
            ClientSecret = null, // Can be null after payment completes
            StripePaymentIntentId = "pi_test_123",
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var request = new GetPaymentByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<PaymentResponse>>;
        ok.Should().NotBeNull();
        ok!.Value!.First().ClientSecret.Should().BeNull();
    }

    [Fact]
    public async Task Given_PaymentsWithDifferentAmounts_When_Handle_Then_ReturnsPreciseAmounts()
    {
        // Arrange
        var user = await SeedUser();
        
        var order1 = await SeedOrder(user, 19.99m);
        var order2 = await SeedOrder(user, 0.01m);
        var order3 = await SeedOrder(user, 999.99m);

        await SeedPayment(order1);
        await SeedPayment(order2);
        await SeedPayment(order3);

        var request = new GetPaymentByUserIdRequest(user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<List<PaymentResponse>>;
        ok.Should().NotBeNull();
        ok!.Value!.Should().Contain(p => p.Amount == 19.99m);
        ok.Value.Should().Contain(p => p.Amount == 0.01m);
        ok.Value.Should().Contain(p => p.Amount == 999.99m);
    }

    #endregion
}

