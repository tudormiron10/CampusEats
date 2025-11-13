using MediatR;

namespace CampusEats.Api.Features.User.Request;

public record GetAllUserRequest() : IRequest<IResult>;

