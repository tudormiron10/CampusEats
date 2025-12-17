using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Validators.User;
using System.Security.Cryptography;

namespace CampusEats.Api.Features.User.Handler;

public class ChangePasswordHandler : IRequestHandler<ChangePasswordRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public ChangePasswordHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var validator = new ChangePasswordValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return ApiErrors.ValidationFailed(validationResult.Errors.First().ErrorMessage);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

        if (user == null)
            return ApiErrors.UserNotFound();

        if (!VerifyPasswordHash(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
            return ApiErrors.IncorrectPassword();

        CreatePasswordHash(request.NewPassword, out byte[] newPasswordHash, out byte[] newPasswordSalt);

        user.PasswordHash = newPasswordHash;
        user.PasswordSalt = newPasswordSalt;

        await _context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Password changed successfully" });
    }

    private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
    {
        using var hmac = new HMACSHA512(passwordSalt);
        var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return computedHash.SequenceEqual(passwordHash);
    }

    private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using var hmac = new HMACSHA512();
        passwordSalt = hmac.Key;
        passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
    }
}