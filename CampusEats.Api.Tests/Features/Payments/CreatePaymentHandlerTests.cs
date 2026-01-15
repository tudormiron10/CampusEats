using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Features.Payments;
using CampusEats.Api.Features.Payments.Response;
using CampusEats.Api.Infrastructure.Extensions;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;
using PaymentEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Payment;

namespace CampusEats.Api.Tests.Features.Payments;

public class CreatePaymentHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private IConfiguration _configuration = null!;
    private ILogger<CreatePaymentHandler> _logger = null!;

    public CreatePaymentHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        SetupDependencies();
    }

    private void SetupDependencies()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        _configuration = Substitute.For<IConfiguration>();
        _configuration["Stripe:SecretKey"].Returns("sk_test_fake_key_for_testing");

        _logger = Substitute.For<ILogger<CreatePaymentHandler>>();
    }

    private CreatePaymentHandler CreateSUT()
    {
        return new CreatePaymentHandler(_context, _configuration, _logger);
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

    private async Task<UserEntity> SeedUser()
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = $"test{Guid.NewGuid():N}@example.com",
            Role = UserRole.Client,
            PasswordHash = new byte[] { 1 },
            PasswordSalt = new byte[] { 2 }
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<OrderEntity> SeedOrder(UserEntity user, OrderStatus status, decimal totalAmount)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            TotalAmount = totalAmount,
            OrderDate = DateTime.UtcNow
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    private async Task<PaymentEntity> SeedPayment(Guid orderId, PaymentStatus status, decimal amount)
    {
        var payment = new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = status,
            StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
            ClientSecret = $"secret_{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Given_EmptyOrderId_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(Guid.Empty);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequest<List<FluentValidation.Results.ValidationFailure>>>();
    }

    #endregion

    #region Order Not Found Tests

    [Fact]
    public async Task Given_NonExistentOrderId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(Guid.NewGuid());

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value.Should().NotBeNull();
        notFound.Value!.Code.Should().Be("NOT_FOUND");
    }

    #endregion

    #region Order Status Tests

    [Fact]
    public async Task Given_CompletedOrder_When_Handle_Then_ReturnsInvalidOperation()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Completed, 50.00m);
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(order.OrderId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().NotBeNull();
        badRequest.Value!.Message.Should().Contain("not pending");
    }

    [Fact]
    public async Task Given_CancelledOrder_When_Handle_Then_ReturnsInvalidOperation()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Cancelled, 50.00m);
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(order.OrderId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<ApiError>;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().NotBeNull();
        badRequest.Value!.Message.Should().Contain("not pending");
    }

    #endregion

    #region Existing Payment Tests

    [Fact]
    public async Task Given_ExistingProcessingPayment_When_Handle_Then_ReturnsExistingPayment()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Pending, 75.00m);
        var existingPayment = await SeedPayment(order.OrderId, PaymentStatus.Processing, 75.00m);
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(order.OrderId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaymentResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.PaymentId.Should().Be(existingPayment.PaymentId);
        ok.Value.Amount.Should().Be(75.00m);
    }

    [Fact]
    public async Task Given_ExistingSucceededPayment_When_Handle_Then_ReturnsExistingPayment()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Pending, 100.00m);
        var existingPayment = await SeedPayment(order.OrderId, PaymentStatus.Succeeded, 100.00m);
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(order.OrderId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaymentResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.PaymentId.Should().Be(existingPayment.PaymentId);
    }

    [Fact]
    public async Task Given_ExistingFailedPayment_When_Handle_Then_ProceedsToCreateNew()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Pending, 50.00m);
        // Failed payment should be ignored, allowing new payment creation
        await SeedPayment(order.OrderId, PaymentStatus.Failed, 50.00m);
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(order.OrderId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert - Will fail at Stripe API call with fake key, but this proves it tried to create new payment
        // (didn't return existing failed payment)
        var problem = result as ProblemHttpResult;
        problem.Should().NotBeNull(); // Stripe call fails with fake key
    }

    [Fact]
    public async Task Given_ExistingCancelledPayment_When_Handle_Then_ProceedsToCreateNew()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Pending, 50.00m);
        // Cancelled payment should be ignored, allowing new payment creation
        await SeedPayment(order.OrderId, PaymentStatus.Cancelled, 50.00m);
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(order.OrderId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert - Will fail at Stripe API call with fake key, proving it tried to create new
        var problem = result as ProblemHttpResult;
        problem.Should().NotBeNull();
    }

    #endregion

    #region Stripe Error Tests

    [Fact]
    public async Task Given_InvalidStripeKey_When_Handle_Then_ReturnsProblem()
    {
        // Arrange
        var user = await SeedUser();
        var order = await SeedOrder(user, OrderStatus.Pending, 25.00m);
        var handler = CreateSUT();
        var request = new CreatePaymentRequest(order.OrderId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert - With fake Stripe key, the API call will fail
        var problem = result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(500);
    }

    #endregion
}

