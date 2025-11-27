using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using CampusEats.Api.Validators.User;

namespace CampusEats.Api.Features.User.Handler;

public class LoginHandler : IRequestHandler<LoginRequest, IResult>
{
    private readonly CampusEatsDbContext _context;
    private readonly IConfiguration _config;

    public LoginHandler(CampusEatsDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config; // Injectăm IConfiguration pentru a citi cheia secretă
    }

    public async Task<IResult> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        var validator = new LoginValidator();
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return ApiErrors.ValidationFailed(validationResult.Errors.First().ErrorMessage);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null || !VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
            return ApiErrors.InvalidCredentials();

        string token = CreateToken(user);

        var userResponse = new UserResponse(
            user.UserId,
            user.Name,
            user.Email,
            user.Role.ToString(),
            user.Loyalty?.Points 
        );

        var loginResponse = new LoginResponse(token, userResponse);

        return Results.Ok(loginResponse);
    }

   
    private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512(passwordSalt))
        {
            var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            // Comparăm hash-ul calculat cu cel din baza de date
            return computedHash.SequenceEqual(passwordHash);
        }
    }
    
    
    private string CreateToken(Infrastructure.Persistence.Entities.User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString()) 
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config.GetSection("AppSettings:Token").Value!));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.Now.AddDays(1), 
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}