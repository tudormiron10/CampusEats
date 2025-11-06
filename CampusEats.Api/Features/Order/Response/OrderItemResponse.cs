namespace CampusEats.Api.Features.Orders;

// DTO "plat" pentru un articol de meniu, pentru a evita ciclul MenuItem -> Order
public record OrderItemResponse(Guid MenuItemId, string Name, decimal Price);