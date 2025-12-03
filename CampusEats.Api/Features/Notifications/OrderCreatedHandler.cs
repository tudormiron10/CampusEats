using CampusEats.Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace CampusEats.Api.Features.Notifications;

/// <summary>
/// Handles OrderCreatedNotification by sending SignalR messages
/// to kitchen staff so they see new orders in real-time.
/// </summary>
public class OrderCreatedHandler : INotificationHandler<OrderCreatedNotification>
{
    private readonly IHubContext<OrderHub, IOrderHubClient> _hubContext;
    private readonly ILogger<OrderCreatedHandler> _logger;

    public OrderCreatedHandler(
        IHubContext<OrderHub, IOrderHubClient> hubContext,
        ILogger<OrderCreatedHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        var newOrder = new NewOrderNotification(
            notification.OrderId,
            notification.UserId,
            notification.CustomerName,
            "Pending",
            notification.TotalAmount,
            notification.OrderDate,
            notification.Items.Select(i => new OrderItemInfo(
                i.MenuItemId,
                i.Name,
                i.Quantity,
                i.UnitPrice
            )).ToList()
        );

        // Notify kitchen staff of new order
        try
        {
            await _hubContext.Clients
                .Group("kitchen")
                .NewOrder(newOrder);

            _logger.LogDebug("Sent new order notification to kitchen for order {OrderId} (Customer: {CustomerName})",
                notification.OrderId, notification.CustomerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send new order notification to kitchen for order {OrderId}",
                notification.OrderId);
        }
    }
}