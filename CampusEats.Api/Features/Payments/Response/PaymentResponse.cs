namespace CampusEats.Api.Features.Payments;

// DTO-ul "plat" pentru răspunsurile API-ului de plăți
public record PaymentResponse(
    Guid PaymentId,
    Guid OrderId, // Doar ID-ul, nu toată entitatea
    decimal Amount,
    string Status // Trimitem statusul ca string
);