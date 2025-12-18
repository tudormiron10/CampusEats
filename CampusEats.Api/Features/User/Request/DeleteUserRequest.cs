using MediatR;

namespace CampusEats.Api.Features.User.Request;

public record DeleteUserRequest(Guid UserId, Guid CurrentUserId) : IRequest<IResult>;
