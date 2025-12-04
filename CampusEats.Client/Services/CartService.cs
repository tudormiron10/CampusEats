using CampusEats.Client.Models;

namespace CampusEats.Client.Services;

public class CartService
{
    private List<CartItem> _items = new();
    private List<PendingOffer> _pendingOffers = new();
    public event Action? OnChange;

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
    public IReadOnlyList<PendingOffer> PendingOffers => _pendingOffers.AsReadOnly();

    public int TotalItems => _items.Sum(i => i.Quantity);

    /// <summary>
    /// Total amount for PAID items only (excludes redeemed items)
    /// </summary>
    public decimal TotalAmount => _items.Where(i => !i.IsRedeemed).Sum(i => i.Price * i.Quantity);

    /// <summary>
    /// Check if cart has any pending offers to redeem
    /// </summary>
    public bool HasPendingOffers => _pendingOffers.Any();


    public void AddItem(MenuItemResponse menuItem)
    {
        var existing = _items.FirstOrDefault(i => i.MenuItemId == menuItem.MenuItemId && !i.IsRedeemed);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            _items.Add(new CartItem
            {
                MenuItemId = menuItem.MenuItemId,
                Name = menuItem.Name,
                Price = menuItem.Price,
                Quantity = 1,
                IsRedeemed = false
            });
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Add a pending offer to cart (will be redeemed at checkout)
    /// </summary>
    public void AddPendingOffer(Guid offerId, string offerTitle, int pointCost, List<(Guid MenuItemId, string Name, int Quantity)> items)
    {
        // Add pending offer
        _pendingOffers.Add(new PendingOffer
        {
            OfferId = offerId,
            Title = offerTitle,
            PointCost = pointCost
        });

        // Add items as redeemed (price 0)
        foreach (var item in items)
        {
            _items.Add(new CartItem
            {
                MenuItemId = item.MenuItemId,
                Name = $"🎁 {item.Name} (Redeemed)",
                Price = 0,
                Quantity = item.Quantity,
                IsRedeemed = true,
                OfferId = offerId
            });
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Get all pending offer IDs
    /// </summary>
    public List<Guid> GetPendingOfferIds() => _pendingOffers.Select(o => o.OfferId).ToList();

    /// <summary>
    /// Clear pending offers after successful checkout
    /// </summary>
    public void ClearPendingOffers()
    {
        _pendingOffers.Clear();
    }

    public void RemoveItem(Guid menuItemId)
    {
        _items.RemoveAll(i => i.MenuItemId == menuItemId && !i.IsRedeemed);
        NotifyStateChanged();
    }

    public void RemoveCartItem(CartItem item)
    {
        _items.Remove(item);
        
        // If removing a redeemed item, also remove the pending offer if no more items from it
        if (item.IsRedeemed && item.OfferId.HasValue)
        {
            var hasMoreItemsFromOffer = _items.Any(i => i.OfferId == item.OfferId);
            if (!hasMoreItemsFromOffer)
            {
                _pendingOffers.RemoveAll(o => o.OfferId == item.OfferId);
            }
        }
        
        NotifyStateChanged();
    }

    public void UpdateQuantity(Guid menuItemId, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.MenuItemId == menuItemId && !i.IsRedeemed);
        if (item != null)
        {
            if (quantity <= 0)
                _items.Remove(item);
            else
                item.Quantity = quantity;

            NotifyStateChanged();
        }
    }

    public void Clear()
    {
        _items.Clear();
        _pendingOffers.Clear();
        NotifyStateChanged();
    }

    /// <summary>
    /// Get menu item IDs for PAID items only (for order creation)
    /// </summary>
    public List<Guid> GetPaidMenuItemIds()
    {
        var ids = new List<Guid>();
        foreach (var item in _items.Where(i => !i.IsRedeemed))
        {
            for (int i = 0; i < item.Quantity; i++)
            {
                ids.Add(item.MenuItemId);
            }
        }
        return ids;
    }

    /// <summary>
    /// Get menu item IDs for REDEEMED items only
    /// </summary>
    public List<Guid> GetRedeemedMenuItemIds()
    {
        var ids = new List<Guid>();
        foreach (var item in _items.Where(i => i.IsRedeemed))
        {
            for (int i = 0; i < item.Quantity; i++)
            {
                ids.Add(item.MenuItemId);
            }
        }
        return ids;
    }

    /// <summary>
    /// Get ALL menu item IDs (paid + redeemed)
    /// </summary>
    public List<Guid> GetMenuItemIds()
    {
        var ids = new List<Guid>();
        foreach (var item in _items)
        {
            for (int i = 0; i < item.Quantity; i++)
            {
                ids.Add(item.MenuItemId);
            }
        }
        return ids;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

public class CartItem
{
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public bool IsRedeemed { get; set; }
    public Guid? OfferId { get; set; }
}

public class PendingOffer
{
    public Guid OfferId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int PointCost { get; set; }
}

