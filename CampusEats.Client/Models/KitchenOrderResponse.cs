using System.Text.Json.Serialization;

namespace CampusEats.Client.Models;

public record KitchenOrderResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("items")] List<KitchenOrderItemResponse> Items
);

public record KitchenOrderItemResponse(
    [property: JsonPropertyName("menuItemId")] Guid? MenuItemId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice
);