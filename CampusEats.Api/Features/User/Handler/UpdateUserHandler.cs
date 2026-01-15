using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
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

            var validator = new UpdateUserValidator();
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return ApiErrors.ValidationFailed(validationResult.Errors[0].ErrorMessage);

            var user = await _context.Users
                .Include(u => u.Loyalty)
                .Include(u => u.Orders)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user == null)
                return ApiErrors.UserNotFound();

            if (user.Email != request.Email)
            {
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email == request.Email && u.UserId != userId, cancellationToken);
                if (emailExists)
                    return ApiErrors.EmailAlreadyExists();
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
                user.Loyalty?.CurrentPoints,
                user.CreatedAt,
                user.Orders.Count
            );

            return Results.Ok(response);
        }
    }
}