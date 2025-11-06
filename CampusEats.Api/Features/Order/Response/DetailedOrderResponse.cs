namespace CampusEats.Api.Features.Orders;

public record DetailedOrderResponse(
    Guid OrderId,
    Guid UserId,
    string Status,
    decimal TotalAmount,
    DateTime OrderDate,
    List<OrderItemResponse> Items
);