using MediatR;

namespace CampusEats.Api.Features.Admin.Request;

public record GetAdminStatsRequest() : IRequest<IResult>;