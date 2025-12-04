namespace CampusEats.Client.Models;

/// <summary>
/// Request to create a payment for an order
/// </summary>
public record CreatePaymentRequest(Guid OrderId);

/// <summary>
/// Payment response from the API containing Stripe ClientSecret
/// </summary>
public record PaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string? ClientSecret
);

/// <summary>
/// Payment history item for user's payment records
/// </summary>
public record PaymentHistoryResponse(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

