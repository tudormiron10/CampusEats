namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class Payment
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }

    // Foreign key for Order
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
}