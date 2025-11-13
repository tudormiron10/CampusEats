namespace CampusEats.Api.Features.Orders.Response
{
    public record OrderItemResponse(Guid MenuItemId, string Name, decimal UnitPrice, int Quantity);
}