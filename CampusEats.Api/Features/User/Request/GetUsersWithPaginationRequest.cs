using MediatR;

namespace CampusEats.Api.Features.User.Request;

public record GetUsersWithPaginationRequest(
    int Page,
    int PageSize,
    string? Search,
    string? RoleFilter
) : IRequest<IResult>;