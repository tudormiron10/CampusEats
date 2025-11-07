namespace CampusEats.Client.Models;

public record OrderResponse(
    Guid OrderId,
    Guid UserId,
    string Status,
    decimal TotalAmount,
    DateTime OrderDate,
    List<OrderItemResponse> Items
);

public record OrderItemResponse(
    Guid MenuItemId,
    string Name,
    decimal Price
);