namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class Order
{
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }

    // Foreign key for User
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Many-to-many relationship (EF Core will create a join table)
    public List<MenuItem> Items { get; set; } = new();
    
    // 1-to-many relationship (An order can have multiple payment attempts)
    public List<Payment> Payments { get; set; } = new();
}