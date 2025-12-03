namespace CampusEats.Client.Models;

/// <summary>
/// DTO for order status change notifications from SignalR.
/// </summary>
public record OrderStatusUpdate(
    Guid OrderId,
    Guid UserId,
    string Status,
    string? PreviousStatus,
    DateTime UpdatedAt
);

/// <summary>
/// DTO for new order notifications from SignalR.
/// Sent to kitchen staff when a customer places an order.
/// </summary>
public record NewOrderNotification(
    Guid OrderId,
    Guid UserId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    DateTime OrderDate,
    List<OrderItemInfo> Items
);

/// <summary>
/// Order item info for new order notifications.
/// </summary>
public record OrderItemInfo(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice
);