using CampusEats.Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace CampusEats.Api.Features.Notifications;

/// <summary>
/// Handles OrderStatusChangedNotification by sending SignalR messages
/// to the order owner and kitchen staff.
/// </summary>
public class OrderStatusChangedHandler : INotificationHandler<OrderStatusChangedNotification>
{
    private readonly IHubContext<OrderHub, IOrderHubClient> _hubContext;
    private readonly ILogger<OrderStatusChangedHandler> _logger;

    public OrderStatusChangedHandler(
        IHubContext<OrderHub, IOrderHubClient> hubContext,
        ILogger<OrderStatusChangedHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(OrderStatusChangedNotification notification, CancellationToken cancellationToken)
    {
        var update = new OrderStatusUpdate(
            notification.OrderId,
            notification.UserId,
            notification.NewStatus,
            notification.OldStatus,
            notification.ChangedAt
        );

        // Send to the order owner (if user exists)
        if (notification.UserId.HasValue)
        {
            try
            {
                await _hubContext.Clients
                    .Group($"user:{notification.UserId}")
                    .OrderStatusChanged(update);

                _logger.LogDebug("Sent status update to user {UserId} for order {OrderId}: {OldStatus} -> {NewStatus}",
                    notification.UserId, notification.OrderId, notification.OldStatus, notification.NewStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status update to user {UserId} for order {OrderId}",
                    notification.UserId, notification.OrderId);
            }
        }

        // Also send to kitchen staff
        try
        {
            await _hubContext.Clients
                .Group("kitchen")
                .OrderStatusChanged(update);

            _logger.LogDebug("Sent status update to kitchen for order {OrderId}", notification.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send status update to kitchen for order {OrderId}", notification.OrderId);
        }
    }
}