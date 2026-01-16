﻿using System;

namespace CampusEats.Api.Infrastructure.Persistence.Entities
{
    public class OrderItem
    {
        public Guid OrderItemId { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;

        // Nullable to allow menu item deletion while preserving order history
        // When a MenuItem is deleted, this becomes null but UnitPrice preserves the price at time of order
        public Guid? MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}

