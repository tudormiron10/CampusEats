using MediatR;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Payments.Request;

public record PaymentConfirmationRequest(Guid PaymentId, PaymentStatus NewStatus) : IRequest<IResult>;
