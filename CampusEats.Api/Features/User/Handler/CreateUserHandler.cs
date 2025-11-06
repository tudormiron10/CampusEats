// Features/Users/CreateUserHandler.cs
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Users;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Users;

public class CreateUserHandler
{
    private readonly CampusEatsDbContext _context;

    public CreateUserHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(CreateUserRequest request)
    {
        var validator = new CreateUserValidator();
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.BadRequest(validationResult.Errors);
        }

        // Verificăm dacă email-ul există deja
        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
        {
            return Results.Conflict("An account with this email already exists.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Role = request.Role
        };

        _context.Users.Add(user);

        int? points = null; // Declarăm 'points' ca un întreg care poate fi null

        // REGULĂ DE BUSINESS: Dacă utilizatorul este Client, creăm contul de loialitate
        if (request.Role == UserRole.Client)
        {
            var loyaltyAccount = new Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = user.UserId,
                Points = 0 // Începe cu 0 puncte
            };
            _context.Loyalties.Add(loyaltyAccount);
            
            points = 0; // Setăm valoarea variabilei
        }

        await _context.SaveChangesAsync();

        // --- AICI ESTE MODIFICAREA PENTRU DTO-UL DE RĂSPUNS ---
        // Creăm noul DTO de răspuns
        var response = new UserResponse(
            user.UserId,
            user.Name,
            user.Email,
            user.Role.ToString(), // Convertim enum în string
            points // Folosim variabila 'points'
        );
        
        // Returnăm 'response' (DTO-ul) în loc de 'user' (Entitatea)
        return Results.Created($"/users/{user.UserId}", response);
    }
}