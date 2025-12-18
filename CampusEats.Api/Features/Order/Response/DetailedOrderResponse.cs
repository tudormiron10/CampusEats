namespace CampusEats.Api.Features.Orders.Response;

public record DetailedOrderResponse(
    Guid OrderId,
    Guid? UserId,
    string Status,
    decimal TotalAmount,
    DateTime OrderDate,
    List<OrderItemResponse> Items
);