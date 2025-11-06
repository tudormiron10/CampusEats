using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Users
{
    public class GetUserByIdHandler
    {
        private readonly CampusEatsDbContext _context;
        public GetUserByIdHandler(CampusEatsDbContext context) { _context = context; }

        public async Task<IResult> Handle(Guid userId)
        {
            var user = await _context.Users
                .Include(u => u.Loyalty)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return Results.NotFound("User not found.");
            }

            var response = new UserResponse(
                user.UserId,
                user.Name,
                user.Email,
                user.Role.ToString(),
                user.Loyalty?.Points
            );

            return Results.Ok(response);
        }
    }
}