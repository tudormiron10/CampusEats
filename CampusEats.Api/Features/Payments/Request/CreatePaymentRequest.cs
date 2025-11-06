// Features/Payments/CreatePaymentRequest.cs
namespace CampusEats.Api.Features.Payments;

// Pentru a iniția o plată, avem nevoie doar de ID-ul comenzii
public record CreatePaymentRequest(Guid OrderId);