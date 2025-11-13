using MediatR;

namespace CampusEats.Api.Features.User.Request;

public record LoginRequest(string Email, string Password) : IRequest<IResult>;