
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Orders;

// Trimitem noul status în corpul (body) request-ului
public record UpdateOrderRequest(OrderStatus NewStatus);