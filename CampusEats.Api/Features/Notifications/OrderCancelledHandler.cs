using CampusEats.Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace CampusEats.Api.Features.Notifications;

/// <summary>
/// Handles OrderCancelledNotification by sending SignalR messages
/// to kitchen staff so cancelled orders are removed in real-time.
/// </summary>
public class OrderCancelledHandler : INotificationHandler<OrderCancelledNotification>
{
    private readonly IHubContext<OrderHub, IOrderHubClient> _hubContext;
    private readonly ILogger<OrderCancelledHandler> _logger;

    public OrderCancelledHandler(
        IHubContext<OrderHub, IOrderHubClient> hubContext,
        ILogger<OrderCancelledHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(OrderCancelledNotification notification, CancellationToken cancellationToken)
    {
        // Notify kitchen staff order has been cancelled and removed
        try
        {
            await _hubContext.Clients
                .Group("kitchen")
                .OrderCancelled(notification.OrderId);

            _logger.LogDebug("Sent cancellation notification to kitchen for order {OrderId}", notification.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send cancellation notification to kitchen for order {OrderId}",
                notification.OrderId);
        }
    }
}