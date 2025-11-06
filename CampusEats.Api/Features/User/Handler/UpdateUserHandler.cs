using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Validators.Users;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Users
{
    public class UpdateUserHandler
    {
        private readonly CampusEatsDbContext _context;

        public UpdateUserHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(Guid userId, UpdateUserRequest request)
        {
            // 1. Validare
            var validator = new UpdateUserValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            // 2. Găsim utilizatorul, incluzând datele de loialitate
            var user = await _context.Users
                .Include(u => u.Loyalty)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return Results.NotFound("User not found.");
            }

            // 3. Verificăm unicitatea email-ului (dacă s-a schimbat)
            if (user.Email != request.Email)
            {
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email == request.Email && u.UserId != userId);
                if (emailExists)
                {
                    return Results.Conflict("An account with this email already exists.");
                }
            }

            // 4. Aplicăm actualizările
            user.Name = request.Name;
            user.Email = request.Email;
            user.Role = request.Role;
            
            await _context.SaveChangesAsync();

            // 5. Creăm și returnăm UserResponse
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