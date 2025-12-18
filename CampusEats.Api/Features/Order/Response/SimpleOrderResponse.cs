namespace CampusEats.Api.Features.Orders.Response
{
    public record SimpleOrderResponse(
        Guid OrderId,
        Guid? UserId,
        string Status,
        decimal TotalAmount,
        DateTime OrderDate
    );
}