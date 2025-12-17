using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Request;
using MediatR;
using CampusEats.Api.Features.User.Response;

namespace CampusEats.Api.Features.Users
{
    public class GetAllUsersHandler : IRequestHandler<GetAllUserRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;
        public GetAllUsersHandler(CampusEatsDbContext context) { _context = context; }

        public async Task<IResult> Handle(GetAllUserRequest request, CancellationToken cancellationToken)
        {
            var usersList = await _context.Users
                .Include(u => u.Loyalty)
                .Include(u => u.Orders)
                .AsNoTracking()
                .Select(user => new UserResponse(
                    user.UserId,
                    user.Name,
                    user.Email,
                    user.Role.ToString(),
                    user.Loyalty != null ? (int?)user.Loyalty.CurrentPoints : null,
                    user.CreatedAt,
                    user.Orders.Count
                ))
                .ToListAsync(cancellationToken);

            return Results.Ok(usersList);
        }
    }
}