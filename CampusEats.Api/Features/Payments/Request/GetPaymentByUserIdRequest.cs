using MediatR;

namespace CampusEats.Api.Features.Payments.Request;

public record GetPaymentByUserIdRequest(Guid UserId) : IRequest<IResult>;

