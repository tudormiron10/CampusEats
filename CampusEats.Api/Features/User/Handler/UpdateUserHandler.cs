using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Validators.Users;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Request;
using MediatR;
using CampusEats.Api.Features.User.Response;

namespace CampusEats.Api.Features.Users
{
    public class UpdateUserHandler : IRequestHandler<UpdateUserRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public UpdateUserHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(UpdateUserRequest request, CancellationToken cancellationToken)
        {
            var userId = request.UserId;

            // 1. Validare
            var validator = new UpdateUserValidator();
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            // 2. Găsim utilizatorul, incluzând datele de loialitate
            var user = await _context.Users
                .Include(u => u.Loyalty)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user == null)
            {
                return Results.NotFound("User not found.");
            }

            // 3. Verificăm unicitatea email-ului (dacă s-a schimbat)
            if (user.Email != request.Email)
            {
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email == request.Email && u.UserId != userId, cancellationToken);
                if (emailExists)
                {
                    return Results.Conflict("An account with this email already exists.");
                }
            }

            user.Name = request.Name;
            user.Email = request.Email;
            user.Role = request.Role;
            
            await _context.SaveChangesAsync(cancellationToken);

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