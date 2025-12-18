namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class Order
{
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }

    // Foreign key for User (nullable to preserve orders when user is deleted)
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    // Replace many-to-many items (MenuItem) with order lines
    public List<OrderItem> Items { get; set; } = new();
    
    // 1-to-many relationship (An order can have multiple payment attempts)
    public List<Payment> Payments { get; set; } = new();
}