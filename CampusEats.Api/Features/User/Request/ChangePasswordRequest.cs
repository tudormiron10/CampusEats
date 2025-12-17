using MediatR;

namespace CampusEats.Api.Features.User.Request;

public record ChangePasswordRequest(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<IResult>;