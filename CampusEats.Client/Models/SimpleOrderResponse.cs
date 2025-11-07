namespace CampusEats.Client.Models;

public record SimpleOrderResponse(
    Guid OrderId,
    Guid UserId,
    string Status,
    decimal TotalAmount,
    DateTime OrderDate
);