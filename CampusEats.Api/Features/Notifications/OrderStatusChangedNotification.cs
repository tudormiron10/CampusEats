using MediatR;

namespace CampusEats.Api.Features.Notifications;

/// <summary>
/// MediatR notification published when an order's status changes.
/// Used to trigger SignalR updates to connected clients.
/// </summary>
public record OrderStatusChangedNotification(
    Guid OrderId,
    Guid UserId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt
) : INotification;