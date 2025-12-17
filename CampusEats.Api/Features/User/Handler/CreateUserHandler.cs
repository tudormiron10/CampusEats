using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Validators.Users;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;
using MediatR;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Http; 

namespace CampusEats.Api.Features.Users; // Sau .User

public class CreateUserHandler : IRequestHandler<CreateUserRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public CreateUserHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var validator = new CreateUserValidator();
        
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return ApiErrors.ValidationFailed(validationResult.Errors.First().ErrorMessage);

        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
            return ApiErrors.EmailAlreadyExists();

        CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

        var user = new Infrastructure.Persistence.Entities.User
        {
            UserId = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Role = request.Role,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        int? points = null;
        
        if (request.Role == UserRole.Client)
        {
            var loyaltyAccount = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = user.UserId,
                CurrentPoints = 0,
                LifetimePoints = 0
            };
            _context.Loyalties.Add(loyaltyAccount);
            
            points = 0; 
        }

        await _context.SaveChangesAsync(cancellationToken);
        
        var response = new UserResponse(
            user.UserId,
            user.Name,
            user.Email,
            user.Role.ToString(),
            points,
            user.CreatedAt,
            0
        );
        
        
        return Results.Created($"/users/{user.UserId}", response);
    }
    
    
    private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        // Folosim HMACSHA512 pentru hashing. "using" asigură eliberarea resurselor.
        using (var hmac = new HMACSHA512())
        {
            // "Salt"-ul este cheia generată aleatoriu de HMAC
            passwordSalt = hmac.Key;
            // "Hash"-ul este rezultatul aplicării algoritmului pe parolă
            passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        }
    }
}