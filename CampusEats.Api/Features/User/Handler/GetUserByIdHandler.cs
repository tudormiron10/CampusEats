using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;
using MediatR;

namespace CampusEats.Api.Features.Users
{
    public class GetUserByIdHandler : IRequestHandler<GetUserByIdRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;
        public GetUserByIdHandler(CampusEatsDbContext context) { _context = context; }

        public async Task<IResult> Handle(GetUserByIdRequest request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.Loyalty)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

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