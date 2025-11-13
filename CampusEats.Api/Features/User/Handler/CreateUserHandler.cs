using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Users;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Request;
using MediatR;
using System.Threading;

namespace CampusEats.Api.Features.Users;

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
        {
            return Results.BadRequest(validationResult.Errors);
        }

        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
        {
            return Results.Conflict("An account with this email already exists.");
        }

        var user = new Infrastructure.Persistence.Entities.User
        {
            UserId = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Role = request.Role
        };

        _context.Users.Add(user);

        int? points = null;
        
        if (request.Role == UserRole.Client)
        {
            var loyaltyAccount = new Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = user.UserId,
                Points = 0 // Începe cu 0 puncte
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
            points 
        );
        
        
        return Results.Created($"/users/{user.UserId}", response);
    }
}