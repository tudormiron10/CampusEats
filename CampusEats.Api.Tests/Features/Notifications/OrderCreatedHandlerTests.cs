using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using CampusEats.Api.Features.Notifications;
using CampusEats.Api.Hubs;

namespace CampusEats.Api.Tests.Features.Notifications;

public class OrderCreatedHandlerTests
{
    private readonly IHubContext<OrderHub, IOrderHubClient> _hubContext;
    private readonly OrderCreatedHandler _handler;

    public OrderCreatedHandlerTests()
    {
        _hubContext = Substitute.For<IHubContext<OrderHub, IOrderHubClient>>();
        var logger = Substitute.For<ILogger<OrderCreatedHandler>>();

        var clients = Substitute.For<IHubClients<IOrderHubClient>>();
        _hubContext.Clients.Returns(clients);

        _handler = new OrderCreatedHandler(_hubContext, logger);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidNotification_When_Handle_Then_SendsToKitchenGroup()
    {
        // Arrange
        var kitchenClient = Substitute.For<IOrderHubClient>();
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        var notification = new OrderCreatedNotification(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CustomerName: "John Doe",
            TotalAmount: 25.50m,
            OrderDate: DateTime.UtcNow,
            Items: new List<OrderItemNotification>
            {
                new(Guid.NewGuid(), "Burger", 2, 10.00m),
                new(Guid.NewGuid(), "Fries", 1, 5.50m)
            }
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _hubContext.Clients.Received(1).Group("kitchen");
        await kitchenClient.Received(1).NewOrder(Arg.Any<NewOrderNotification>());
    }

    [Fact]
    public async Task Given_Notification_When_Handle_Then_CreatesCorrectNewOrderNotification()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderDate = DateTime.UtcNow;
        var menuItemId = Guid.NewGuid();

        var kitchenClient = Substitute.For<IOrderHubClient>();
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        NewOrderNotification? capturedNotification = null;
        await kitchenClient.NewOrder(Arg.Do<NewOrderNotification>(n => capturedNotification = n));

        var notification = new OrderCreatedNotification(
            OrderId: orderId,
            UserId: userId,
            CustomerName: "Jane Smith",
            TotalAmount: 30.00m,
            OrderDate: orderDate,
            Items: new List<OrderItemNotification>
            {
                new(menuItemId, "Pizza", 1, 15.00m),
                new(Guid.NewGuid(), "Drink", 2, 7.50m)
            }
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.OrderId.Should().Be(orderId);
        capturedNotification.UserId.Should().Be(userId);
        capturedNotification.CustomerName.Should().Be("Jane Smith");
        capturedNotification.Status.Should().Be("Pending");
        capturedNotification.TotalAmount.Should().Be(30.00m);
        capturedNotification.OrderDate.Should().Be(orderDate);
    }

    [Fact]
    public async Task Given_NotificationWithItems_When_Handle_Then_MapsItemsCorrectly()
    {
        // Arrange
        var menuItemId1 = Guid.NewGuid();
        var menuItemId2 = Guid.NewGuid();

        var kitchenClient = Substitute.For<IOrderHubClient>();
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        NewOrderNotification? capturedNotification = null;
        await kitchenClient.NewOrder(Arg.Do<NewOrderNotification>(n => capturedNotification = n));

        var notification = new OrderCreatedNotification(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CustomerName: "Test Customer",
            TotalAmount: 45.00m,
            OrderDate: DateTime.UtcNow,
            Items: new List<OrderItemNotification>
            {
                new(menuItemId1, "Burger", 2, 12.00m),
                new(menuItemId2, "Salad", 1, 21.00m)
            }
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Items.Should().HaveCount(2);
        
        var burger = capturedNotification.Items.First(i => i.Name == "Burger");
        burger.MenuItemId.Should().Be(menuItemId1);
        burger.Quantity.Should().Be(2);
        burger.UnitPrice.Should().Be(12.00m);
        
        var salad = capturedNotification.Items.First(i => i.Name == "Salad");
        salad.MenuItemId.Should().Be(menuItemId2);
        salad.Quantity.Should().Be(1);
        salad.UnitPrice.Should().Be(21.00m);
    }

    [Fact]
    public async Task Given_NotificationWithEmptyItems_When_Handle_Then_SendsEmptyItemsList()
    {
        // Arrange
        var kitchenClient = Substitute.For<IOrderHubClient>();
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        NewOrderNotification? capturedNotification = null;
        await kitchenClient.NewOrder(Arg.Do<NewOrderNotification>(n => capturedNotification = n));

        var notification = new OrderCreatedNotification(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CustomerName: "Empty Order",
            TotalAmount: 0m,
            OrderDate: DateTime.UtcNow,
            Items: new List<OrderItemNotification>()
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_NotificationWithNullUserId_When_Handle_Then_StillSendsNotification()
    {
        // Arrange
        var kitchenClient = Substitute.For<IOrderHubClient>();
        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        NewOrderNotification? capturedNotification = null;
        await kitchenClient.NewOrder(Arg.Do<NewOrderNotification>(n => capturedNotification = n));

        var notification = new OrderCreatedNotification(
            OrderId: Guid.NewGuid(),
            UserId: null,
            CustomerName: "Guest",
            TotalAmount: 15.00m,
            OrderDate: DateTime.UtcNow,
            Items: new List<OrderItemNotification>
            {
                new(Guid.NewGuid(), "Coffee", 1, 15.00m)
            }
        );

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.UserId.Should().BeNull();
        capturedNotification.CustomerName.Should().Be("Guest");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Given_KitchenGroupThrowsException_When_Handle_Then_DoesNotRethrow()
    {
        // Arrange
        var kitchenClient = Substitute.For<IOrderHubClient>();
        kitchenClient.NewOrder(Arg.Any<NewOrderNotification>())
            .ThrowsAsync(new Exception("SignalR error"));

        _hubContext.Clients.Group("kitchen").Returns(kitchenClient);

        var notification = new OrderCreatedNotification(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CustomerName: "Test",
            TotalAmount: 10.00m,
            OrderDate: DateTime.UtcNow,
            Items: new List<OrderItemNotification>()
        );

        // Act
        var act = async () => await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}

