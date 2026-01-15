using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using CampusEats.Api.Features.Notifications;
using CampusEats.Api.Hubs;

namespace CampusEats.Api.Tests.Features.Notifications;

public class OrderStatusChangedHandlerTests
{
    private readonly IHubContext<OrderHub, IOrderHubClient> _hubContext;
    private readonly OrderStatusChangedHandler _handler;

    public OrderStatusChangedHandlerTests()
    {
        _hubContext = Substitute.For<IHubContext<OrderHub, IOrderHubClient>>();
        var logger = Substitute.For<ILogger<OrderStatusChangedHandler>>();

        var clients = Substitute.For<IHubClients<IOrderHubClient>>();
        _hubContext.Clients.Returns(clients);

        _handler = new OrderStatusChangedHandler(_hubContext, logger);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_NotificationWithUserId_When_Handle_Then_SendsToUserGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userClient = Substitute.For<IOrderHubClient>();
        var kitchenClient = Substitute.For<IOrderHubClient>();
        
        _hubContext.Clients.Group($"user:{userId}").Returns(userClient);
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);
        
        var notification = new OrderStatusChangedNotification(
            OrderId: Guid.NewGuid(),
            UserId: userId,
            OldStatus: "Pending",
            NewStatus: "InPreparation",
            ChangedAt: DateTime.UtcNow
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _hubContext.Clients.Received(1).Group($"user:{userId}");
        await userClient.Received(1).OrderStatusChanged(Arg.Any<OrderStatusUpdate>());
    }

    [Fact]
    public async Task Given_AnyNotification_When_Handle_Then_SendsToKitchenGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userClient = Substitute.For<IOrderHubClient>();
        var kitchenClient = Substitute.For<IOrderHubClient>();
        
        _hubContext.Clients.Group($"user:{userId}").Returns(userClient);
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);
        
        var notification = new OrderStatusChangedNotification(
            OrderId: Guid.NewGuid(),
            UserId: userId,
            OldStatus: "InPreparation",
            NewStatus: "Ready",
            ChangedAt: DateTime.UtcNow
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _hubContext.Clients.Received(1).Group("kitchen");
        await kitchenClient.Received(1).OrderStatusChanged(Arg.Any<OrderStatusUpdate>());
    }

    [Fact]
    public async Task Given_NotificationWithoutUserId_When_Handle_Then_OnlySendsToKitchen()
    {
        // Arrange
        var notification = new OrderStatusChangedNotification(
            OrderId: Guid.NewGuid(),
            UserId: null,
            OldStatus: "Ready",
            NewStatus: "Completed",
            ChangedAt: DateTime.UtcNow
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _hubContext.Clients.DidNotReceive().Group(Arg.Is<string>(s => s.StartsWith("user:")));
        _hubContext.Clients.Received(1).Group("kitchen");
    }

    [Fact]
    public async Task Given_Notification_When_Handle_Then_CreatesCorrectOrderStatusUpdate()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var changedAt = DateTime.UtcNow;
        
        var userClient = Substitute.For<IOrderHubClient>();
        var kitchenClient = Substitute.For<IOrderHubClient>();
        
        _hubContext.Clients.Group($"user:{userId}").Returns(userClient);
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);
        
        var notification = new OrderStatusChangedNotification(
            OrderId: orderId,
            UserId: userId,
            OldStatus: "Pending",
            NewStatus: "Cancelled",
            ChangedAt: changedAt
        );

        OrderStatusUpdate? capturedUpdate = null;
        await userClient.OrderStatusChanged(Arg.Do<OrderStatusUpdate>(u => capturedUpdate = u));

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        capturedUpdate.Should().NotBeNull();
        capturedUpdate!.OrderId.Should().Be(orderId);
        capturedUpdate.UserId.Should().Be(userId);
        capturedUpdate.Status.Should().Be("Cancelled");
        capturedUpdate.PreviousStatus.Should().Be("Pending");
        capturedUpdate.UpdatedAt.Should().Be(changedAt);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Given_UserGroupThrowsException_When_Handle_Then_ContinuesToKitchen()
    {
        // Arrange
        var notification = new OrderStatusChangedNotification(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            OldStatus: "Pending",
            NewStatus: "InPreparation",
            ChangedAt: DateTime.UtcNow
        );

        var userClient = Substitute.For<IOrderHubClient>();
        userClient.OrderStatusChanged(Arg.Any<OrderStatusUpdate>())
            .ThrowsAsync(new Exception("SignalR connection error"));

        var kitchenClient = Substitute.For<IOrderHubClient>();

        _hubContext.Clients.Group($"user:{notification.UserId}").Returns(userClient);
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert - kitchen should still be called
        await kitchenClient.Received(1).OrderStatusChanged(Arg.Any<OrderStatusUpdate>());
    }

    [Fact]
    public async Task Given_KitchenGroupThrowsException_When_Handle_Then_DoesNotRethrow()
    {
        // Arrange
        var notification = new OrderStatusChangedNotification(
            OrderId: Guid.NewGuid(),
            UserId: null,
            OldStatus: "InPreparation",
            NewStatus: "Ready",
            ChangedAt: DateTime.UtcNow
        );

        var kitchenClient = Substitute.For<IOrderHubClient>();
        kitchenClient.OrderStatusChanged(Arg.Any<OrderStatusUpdate>())
            .ThrowsAsync(new Exception("Kitchen SignalR error"));
        
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        // Act
        var act = async () => await _handler.Handle(notification, CancellationToken.None);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_BothGroupsThrowExceptions_When_Handle_Then_DoesNotRethrow()
    {
        // Arrange
        var notification = new OrderStatusChangedNotification(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            OldStatus: "Ready",
            NewStatus: "Completed",
            ChangedAt: DateTime.UtcNow
        );

        var userClient = Substitute.For<IOrderHubClient>();
        userClient.OrderStatusChanged(Arg.Any<OrderStatusUpdate>())
            .ThrowsAsync(new Exception("User error"));

        var kitchenClient = Substitute.For<IOrderHubClient>();
        kitchenClient.OrderStatusChanged(Arg.Any<OrderStatusUpdate>())
            .ThrowsAsync(new Exception("Kitchen error"));

        _hubContext.Clients.Group($"user:{notification.UserId}").Returns(userClient);
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        // Act
        var act = async () => await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region All Order Status Transitions

    [Theory]
    [InlineData("Pending", "InPreparation")]
    [InlineData("InPreparation", "Ready")]
    [InlineData("Ready", "Completed")]
    [InlineData("Pending", "Cancelled")]
    public async Task Given_StatusTransition_When_Handle_Then_SendsNotification(
        string oldStatus, string newStatus)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userClient = Substitute.For<IOrderHubClient>();
        var kitchenClient = Substitute.For<IOrderHubClient>();
        
        _hubContext.Clients.Group($"user:{userId}").Returns(userClient);
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);
        
        var notification = new OrderStatusChangedNotification(
            OrderId: Guid.NewGuid(),
            UserId: userId,
            OldStatus: oldStatus,
            NewStatus: newStatus,
            ChangedAt: DateTime.UtcNow
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await userClient.Received(1).OrderStatusChanged(Arg.Any<OrderStatusUpdate>());
        await kitchenClient.Received(1).OrderStatusChanged(Arg.Any<OrderStatusUpdate>());
    }

    #endregion
}

