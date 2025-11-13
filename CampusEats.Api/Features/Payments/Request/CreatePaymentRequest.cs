using MediatR;

namespace CampusEats.Api.Features.Payments.Request;
public record CreatePaymentRequest(Guid OrderId) : IRequest<IResult>;
