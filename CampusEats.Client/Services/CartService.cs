using CampusEats.Client.Models;

namespace CampusEats.Client.Services;

public class CartService
{
    private List<CartItem> _items = new();
    public event Action? OnChange;

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    public int TotalItems => _items.Sum(i => i.Quantity);

    public decimal TotalAmount => _items.Sum(i => i.Price * i.Quantity);

    public void AddItem(MenuItemResponse menuItem)
    {
        var existing = _items.FirstOrDefault(i => i.MenuItemId == menuItem.MenuItemId);
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
                Quantity = 1
            });
        }
        NotifyStateChanged();
    }

    public void RemoveItem(Guid menuItemId)
    {
        _items.RemoveAll(i => i.MenuItemId == menuItemId);
        NotifyStateChanged();
    }

    public void UpdateQuantity(Guid menuItemId, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.MenuItemId == menuItemId);
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
        NotifyStateChanged();
    }

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
}