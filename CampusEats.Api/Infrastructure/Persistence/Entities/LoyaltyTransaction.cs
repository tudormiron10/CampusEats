namespace CampusEats.Api.Infrastructure.Persistence.Entities;
public class LoyaltyTransaction
{
    public Loyalty Loyalty { get; set; } = null!;
    public Guid LoyaltyId { get; set; }
    public Guid? OrderId { get; set; }
    public int Points { get; set; }
    public string Type { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime Date { get; set; }
    public Guid TransactionId { get; set; }
    
}
