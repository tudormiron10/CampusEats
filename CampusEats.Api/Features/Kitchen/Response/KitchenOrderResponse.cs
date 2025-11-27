using System;
using System.Collections.Generic;

namespace CampusEats.Api.Features.Kitchen.Response
{
    public record KitchenOrderResponse(Guid Id, string Status, DateTime CreatedAt, List<KitchenOrderItemResponse> Items);
}

