using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using CampusEats.Api.Features.Notifications;
using CampusEats.Api.Hubs;

namespace CampusEats.Api.Tests.Features.Notifications;

public class OrderCancelledHandlerTests
{
    private readonly IHubContext<OrderHub, IOrderHubClient> _hubContext;
    private readonly OrderCancelledHandler _handler;

    public OrderCancelledHandlerTests()
    {
        _hubContext = Substitute.For<IHubContext<OrderHub, IOrderHubClient>>();
        var logger = Substitute.For<ILogger<OrderCancelledHandler>>();

        var clients = Substitute.For<IHubClients<IOrderHubClient>>();
        _hubContext.Clients.Returns(clients);

        _handler = new OrderCancelledHandler(_hubContext, logger);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidNotification_When_Handle_Then_SendsToKitchenGroup()
    {
        // Arrange
        var kitchenClient = Substitute.For<IOrderHubClient>();
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        var notification = new OrderCancelledNotification(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CancelledAt: DateTime.UtcNow
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _hubContext.Clients.Received(1).Group("kitchen");
        await kitchenClient.Received(1).OrderCancelled(notification.OrderId);
    }

    [Fact]
    public async Task Given_NotificationWithoutUserId_When_Handle_Then_StillSendsToKitchen()
    {
        // Arrange
        var kitchenClient = Substitute.For<IOrderHubClient>();
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        var notification = new OrderCancelledNotification(
            OrderId: Guid.NewGuid(),
            UserId: null,
            CancelledAt: DateTime.UtcNow
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await kitchenClient.Received(1).OrderCancelled(notification.OrderId);
    }

    [Fact]
    public async Task Given_Notification_When_Handle_Then_SendsCorrectOrderId()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var kitchenClient = Substitute.For<IOrderHubClient>();
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        Guid? capturedOrderId = null;
        await kitchenClient.OrderCancelled(Arg.Do<Guid>(id => capturedOrderId = id));

        var notification = new OrderCancelledNotification(
            OrderId: orderId,
            UserId: Guid.NewGuid(),
            CancelledAt: DateTime.UtcNow
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        capturedOrderId.Should().Be(orderId);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Given_KitchenGroupThrowsException_When_Handle_Then_DoesNotRethrow()
    {
        // Arrange
        var kitchenClient = Substitute.For<IOrderHubClient>();
        kitchenClient.OrderCancelled(Arg.Any<Guid>())
            .ThrowsAsync(new Exception("SignalR error"));

        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        var notification = new OrderCancelledNotification(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CancelledAt: DateTime.UtcNow
        );

        // Act
        var act = async () => await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}

