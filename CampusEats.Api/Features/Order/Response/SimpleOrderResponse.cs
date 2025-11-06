namespace CampusEats.Api.Features.Orders;

public record SimpleOrderResponse(
    Guid OrderId,
    Guid UserId,
    string Status,
    decimal TotalAmount,
    DateTime OrderDate
);