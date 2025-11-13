using MediatR;
using System;

namespace CampusEats.Api.Features.User.Request;

public record GetUserByIdRequest(Guid UserId) : IRequest<IResult>;

