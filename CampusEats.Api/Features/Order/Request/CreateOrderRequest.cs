namespace CampusEats.Api.Features.Orders;

// DTO-ul pentru plasarea unei comenzi
public record CreateOrderRequest(Guid UserId, List<Guid> MenuItemIds);