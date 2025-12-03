namespace CampusEats.Api.Hubs;

/// <summary>
/// Strongly-typed SignalR client interface for order notifications.
/// Provides compile-time safety for hub method calls.
/// </summary>
public interface IOrderHubClient
{
    /// <summary>
    /// Called when an order's status changes (e.g., Pending → InPreparation → Ready → Completed).
    /// </summary>
    Task OrderStatusChanged(OrderStatusUpdate update);

    /// <summary>
    /// Called when a new order is placed. Sent to kitchen group.
    /// </summary>
    Task NewOrder(NewOrderNotification order);

    /// <summary>
    /// Called when an order is cancelled. Sent to kitchen group.
    /// </summary>
    Task OrderCancelled(Guid orderId);
}

/// <summary>
/// DTO for order status change notifications.
/// </summary>
public record OrderStatusUpdate(
    Guid OrderId,
    Guid UserId,
    string Status,
    string? PreviousStatus,
    DateTime UpdatedAt
);

/// <summary>
/// DTO for new order notifications sent to kitchen.
/// Contains full order details needed to display in the kitchen dashboard.
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
/// DTO for order item information in new order notifications.
/// </summary>
public record OrderItemInfo(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice
);