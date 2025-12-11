using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using MediatR;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Features.Kitchen.Handler;
using CampusEats.Api.Features.Notifications;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.Order;

public class UpdateOrderStatusHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private UpdateOrderStatusHandler _handler = null!;
    private IPublisher _publisher = null!;

    public UpdateOrderStatusHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private UpdateOrderStatusHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        _publisher = Substitute.For<IPublisher>();

        return new UpdateOrderStatusHandler(_context, _publisher);
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
            PasswordSalt = new byte[128]
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<OrderEntity> SeedOrder(OrderStatus status = OrderStatus.Pending)
    {
        var user = await SeedUser();
        
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            TotalAmount = 50.00m,
            OrderDate = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return order;
    }

    #region Happy Path - Valid Transitions

    [Fact]
    public async Task Given_PendingOrder_When_TransitionToInPreparation_Then_ReturnsNoContent()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Pending);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.InPreparation);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify persistence
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.InPreparation);
    }

    [Fact]
    public async Task Given_InPreparationOrder_When_TransitionToReady_Then_ReturnsNoContent()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.InPreparation);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Ready);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify persistence
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public async Task Given_ReadyOrder_When_TransitionToCompleted_Then_ReturnsNoContent()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Ready);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Completed);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify persistence
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Given_PendingOrder_When_TransitionToCancelled_Then_ReturnsNoContent()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Pending);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Cancelled);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();

        // Verify persistence
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Given_ValidTransition_When_Handle_Then_PublishesNotification()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Pending);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.InPreparation);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _publisher.Received(1).Publish(
            Arg.Any<OrderStatusChangedNotification>(),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Failure Scenarios - Invalid Transitions

    [Fact]
    public async Task Given_PendingOrder_When_TransitionDirectlyToReady_Then_ReturnsBadRequest()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Pending);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Ready);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();

        // Verify status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Given_PendingOrder_When_TransitionDirectlyToCompleted_Then_ReturnsBadRequest()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Pending);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Completed);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();

        // Verify status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Given_InPreparationOrder_When_TransitionToCompleted_Then_ReturnsBadRequest()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.InPreparation);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Completed);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();

        // Verify status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.InPreparation);
    }

    [Fact]
    public async Task Given_ReadyOrder_When_TransitionToCancelled_Then_ReturnsBadRequest()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Ready);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Cancelled);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();

        // Verify status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public async Task Given_CompletedOrder_When_TransitionToAnyStatus_Then_ReturnsBadRequest()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Completed);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Pending);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();

        // Verify status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Given_CancelledOrder_When_TransitionToAnyStatus_Then_ReturnsBadRequest()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Cancelled);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.InPreparation);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();

        // Verify status unchanged
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Given_NonExistentOrderId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var nonExistentOrderId = Guid.NewGuid();
        var request = new UpdateOrderStatusRequest(nonExistentOrderId, OrderStatus.InPreparation);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
        notFound!.Value!.Code.Should().Be("NOT_FOUND");
        notFound.Value.Message.Should().Contain("Order");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Given_EmptyOrderId_When_Handle_Then_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateOrderStatusRequest(Guid.Empty, OrderStatus.InPreparation);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var notFound = result as NotFound<ApiError>;
        notFound.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_FullOrderLifecycle_When_ValidTransitions_Then_AllSucceed()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Pending);

        // Act & Assert - Pending -> InPreparation
        var result1 = await _handler.Handle(
            new UpdateOrderStatusRequest(order.OrderId, OrderStatus.InPreparation),
            CancellationToken.None);
        result1.Should().BeOfType<NoContent>();

        // Act & Assert - InPreparation -> Ready
        var result2 = await _handler.Handle(
            new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Ready),
            CancellationToken.None);
        result2.Should().BeOfType<NoContent>();

        // Act & Assert - Ready -> Completed
        var result3 = await _handler.Handle(
            new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Completed),
            CancellationToken.None);
        result3.Should().BeOfType<NoContent>();

        // Verify final state
        var savedOrder = await _context.Orders.FindAsync(order.OrderId);
        savedOrder!.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Given_SameStatusTransition_When_Handle_Then_ReturnsBadRequest()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.InPreparation);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.InPreparation);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var badRequest = result as BadRequest<string>;
        badRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_InvalidTransition_When_Handle_Then_DoesNotPublishNotification()
    {
        // Arrange
        var order = await SeedOrder(OrderStatus.Pending);
        var request = new UpdateOrderStatusRequest(order.OrderId, OrderStatus.Completed); // Invalid skip

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert - notification should NOT be published for invalid transitions
        await _publisher.DidNotReceive().Publish(
            Arg.Any<OrderStatusChangedNotification>(),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion
}

