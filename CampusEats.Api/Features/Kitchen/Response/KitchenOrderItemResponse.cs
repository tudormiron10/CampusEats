﻿using System;

namespace CampusEats.Api.Features.Kitchen.Response
{
    public record KitchenOrderItemResponse(Guid? MenuItemId, string Name, int Quantity, decimal UnitPrice);
}

