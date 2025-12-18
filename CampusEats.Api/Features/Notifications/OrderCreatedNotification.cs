using MediatR;

namespace CampusEats.Api.Features.Notifications;

/// <summary>
/// MediatR notification published when a new order is created.
/// Used to notify kitchen staff via SignalR.
/// </summary>
public record OrderCreatedNotification(
    Guid OrderId,
    Guid? UserId,
    string CustomerName,
    decimal TotalAmount,
    DateTime OrderDate,
    List<OrderItemNotification> Items
) : INotification;

/// <summary>
/// Order item details for the notification.
/// </summary>
public record OrderItemNotification(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice
);