using CampusEats.Api.Infrastructure.Persistence.Entities;
using MediatR;

namespace CampusEats.Api.Features.User.Request;

public record UpdateUserRequest(Guid UserId, string Name, string Email, UserRole Role) : IRequest<IResult>;
