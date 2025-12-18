using MediatR;

namespace CampusEats.Api.Features.Notifications;

/// <summary>
/// MediatR notification published when an order is cancelled.
/// Used to notify kitchen staff order was removed from their dashboard.
/// </summary>
public record OrderCancelledNotification(
    Guid OrderId,
    Guid? UserId,
    DateTime CancelledAt
) : INotification;