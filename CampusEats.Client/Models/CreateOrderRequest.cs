namespace CampusEats.Client.Models;

public record CreateOrderRequest(
    Guid UserId,
    List<Guid> MenuItemIds,
    List<Guid>? RedeemedMenuItemIds = null
);