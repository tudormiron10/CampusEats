namespace CampusEats.Api.Infrastructure.Persistence.Entities;

public class Payment
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }

    // Stripe-specific fields
    public string? StripePaymentIntentId { get; set; }
    public string? ClientSecret { get; set; }
    
    // Timestamps for tracking
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Idempotency tracking (prevents processing same webhook event twice)
    public string? StripeEventId { get; set; }

    // Foreign key for Order
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
}