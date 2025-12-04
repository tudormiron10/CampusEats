namespace CampusEats.Api.Features.Loyalty.Response;

public record LoyaltyTransactionResponse(
    Guid TransactionId,
    DateTime Date,
    string Description,
    string Type,
    int Points,
    Guid? OrderId
);
