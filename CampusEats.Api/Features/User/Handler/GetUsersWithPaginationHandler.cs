using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.User.Handler;

public class GetUsersWithPaginationHandler : IRequestHandler<GetUsersWithPaginationRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetUsersWithPaginationHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetUsersWithPaginationRequest request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Active order statuses
        var activeStatuses = new[] { OrderStatus.Pending, OrderStatus.InPreparation, OrderStatus.Ready };

        // Build query
        var query = _context.Users.AsNoTracking();

        // Apply search filter (name or email)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(u =>
                u.Name.ToLower().Contains(searchLower) ||
                u.Email.ToLower().Contains(searchLower));
        }

        // Apply role filter
        if (!string.IsNullOrWhiteSpace(request.RoleFilter) &&
            Enum.TryParse<UserRole>(request.RoleFilter, true, out var role))
        {
            query = query.Where(u => u.Role == role);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Apply pagination and project to response
        var users = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserResponse(
                u.UserId,
                u.Name,
                u.Email,
                u.Role.ToString(),
                u.Loyalty != null ? u.Loyalty.CurrentPoints : null,
                u.CreatedAt,
                u.Orders.Count,
                u.Orders.OrderByDescending(o => o.OrderDate).Select(o => (DateTime?)o.OrderDate).FirstOrDefault(),
                u.Orders.Any(o => activeStatuses.Contains(o.Status))
            ))
            .ToListAsync(cancellationToken);

        var response = new PaginatedUsersResponse(
            users,
            totalCount,
            page,
            pageSize,
            totalPages
        );

        return Results.Ok(response);
    }
}