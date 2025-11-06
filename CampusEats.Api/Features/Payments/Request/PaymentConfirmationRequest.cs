// Features/Payments/PaymentWebhookRequest.cs
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Payments;

// Acesta simulează un payload de la un webhook (ex. Stripe)
// Trimitem PaymentId și noul status (Successful sau Failed)
public record PaymentConfirmationRequest(Guid PaymentId, PaymentStatus NewStatus);