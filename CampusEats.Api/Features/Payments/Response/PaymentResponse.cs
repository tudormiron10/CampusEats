namespace CampusEats.Api.Features.Payments.Response;

public record PaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string? ClientSecret  
);