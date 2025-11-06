using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Users
{
    public class GetAllUsersHandler
    {
        private readonly CampusEatsDbContext _context;
        public GetAllUsersHandler(CampusEatsDbContext context) { _context = context; }

        public async Task<IResult> Handle()
        {
            var usersList = await _context.Users
                .Include(u => u.Loyalty)
                .AsNoTracking()
                .Select(user => new UserResponse(
                    user.UserId,
                    user.Name,
                    user.Email,
                    user.Role.ToString(),
                    user.Loyalty != null ? (int?)user.Loyalty.Points : null
                ))
                .ToListAsync();

            return Results.Ok(usersList);
        }
    }
}