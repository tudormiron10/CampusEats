﻿using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Tests.Features.User
{
    public class LoginHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private CampusEats.Api.Features.User.Handler.LoginHandler _handler = null!;
        private IConfiguration _config = null!;

        public LoginHandlerTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private CampusEats.Api.Features.User.Handler.LoginHandler CreateSUT()
        {
            // setup in-memory db
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            // mock configuration to return a long token string
            _config = Substitute.For<IConfiguration>();
            var section = Substitute.For<IConfigurationSection>();
            var longKey = new string('K', 128); // >= 64 chars for HMACSHA512
            section.Value.Returns(longKey);
            _config.GetSection("AppSettings:Token").Returns(section);

            return new CampusEats.Api.Features.User.Handler.LoginHandler(_context, _config);
        }

        public void Dispose()
        {
            if (_context != null)
            {
                _context.Database.EnsureDeleted();
                _context.Dispose();
                _context = null!;
            }
        }

        [Fact]
        public async Task Given_ValidCredentials_When_Handle_Then_ReturnsOkWithTokenAndUser()
        {
            // Arrange
            var email = "login@example.com";
            var plainPassword = "Password123";

            // create password hash/salt compatible with VerifyPasswordHash (HMACSHA512(passwordSalt) usage)
            byte[] passwordSalt;
            byte[] passwordHash;
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainPassword));
            }

            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Login User",
                Email = email,
                Role = UserRole.Client,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest(email, plainPassword);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<LoginResponse>;
            ok.Should().NotBeNull();

            var loginResponse = ok!.Value;
            loginResponse.Should().NotBeNull();
            loginResponse.Token.Should().NotBeNullOrWhiteSpace();

            // depending on LoginResponse shape use property name for nested user (common is .User or .UserResponse)
            // try common property names: User (preferred) else UserResponse
            var returnedUser = loginResponse.GetType().GetProperty("User")?.GetValue(loginResponse)
                               ?? loginResponse.GetType().GetProperty("UserResponse")?.GetValue(loginResponse);

            returnedUser.Should().NotBeNull();
            var returnedEmail = returnedUser!.GetType().GetProperty("Email")!.GetValue(returnedUser) as string;
            returnedEmail.Should().Be(email);
        }

        [Fact]
        public async Task Given_WrongPassword_When_Handle_Then_ReturnsUnauthorized()
        {
            // Arrange
            var email = "wrongpass@example.com";
            var correctPassword = "Correct123";
            var wrongPassword = "NotCorrect";

            byte[] passwordSalt;
            byte[] passwordHash;
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(correctPassword));
            }

            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "WrongPass User",
                Email = email,
                Role = UserRole.Client,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest(email, wrongPassword);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            var statusCodeResult = result as IStatusCodeHttpResult;
            statusCodeResult.Should().NotBeNull();
            statusCodeResult!.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task Given_NonExistentUser_When_Handle_Then_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequest("missing@example.com", "Whatever123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            var statusCodeResult = result as IStatusCodeHttpResult;
            statusCodeResult.Should().NotBeNull();
            statusCodeResult!.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task Given_InvalidRequest_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var request = new LoginRequest("", "");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");
        }

        [Fact]
        public async Task Given_ValidCredentials_When_Handle_Then_TokenContainsCorrectRoleAndName()
        {
            // Arrange
            var email = "claims@example.com";
            var plainPassword = "Password123";
            var name = "John Doe";
            var role = UserRole.Client;

            byte[] passwordSalt;
            byte[] passwordHash;
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainPassword));
            }

            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = name,
                Email = email,
                Role = role,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest(email, plainPassword);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<LoginResponse>;
            ok.Should().NotBeNull();
            var loginResponse = ok!.Value;
            loginResponse.Should().NotBeNull();
            loginResponse.Token.Should().NotBeNullOrWhiteSpace();

            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(loginResponse.Token);

            var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role);
            roleClaim.Should().NotBeNull();
            roleClaim!.Value.Should().Be(role.ToString());

            var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == System.Security.Claims.ClaimTypes.Name);
            nameClaim.Should().NotBeNull();
            nameClaim!.Value.Should().Be(name);
        }

        [Fact]
        public async Task Given_CorrectPasswordButWrongCase_When_Handle_Then_ReturnsUnauthorized()
        {
            // Arrange
            var email = "casepwd@example.com";
            var storedPassword = "Password123";
            var loginPassword = "password123"; // different casing

            byte[] passwordSalt;
            byte[] passwordHash;
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(storedPassword));
            }

            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Case Pwd User",
                Email = email,
                Role = UserRole.Client,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest(email, loginPassword);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            var statusCodeResult = result as IStatusCodeHttpResult;
            statusCodeResult.Should().NotBeNull();
            statusCodeResult!.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task Given_PasswordWithWhitespace_When_Handle_Then_DoesNotTrimAndMatchesExactly()
        {
            // Arrange
            var email = "whitespace@example.com";
            var storedPassword = " secret "; // includes spaces
            var loginPassword = "secret";      // trimmed version

            byte[] passwordSalt;
            byte[] passwordHash;
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(storedPassword));
            }

            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Whitespace User",
                Email = email,
                Role = UserRole.Client,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest(email, loginPassword);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            var statusCodeResult = result as IStatusCodeHttpResult;
            statusCodeResult.Should().NotBeNull();
            statusCodeResult!.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task Given_ClientWithLoyalty_When_Handle_Then_ReturnsUserWithCorrectLoyaltyPoints()
        {
            // Arrange
            var email = "loyaltylogin@example.com";
            var plainPassword = "Password123";

            byte[] passwordSalt;
            byte[] passwordHash;
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainPassword));
            }

            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Loyalty Login User",
                Email = email,
                Role = UserRole.Client,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
            };

            var loyalty = new Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = user.UserId,
                CurrentPoints = 100,
                LifetimePoints = 100
            };

            user.Loyalty = loyalty;

            _context.Users.Add(user);
            _context.Loyalties.Add(loyalty);
            await _context.SaveChangesAsync();

            var request = new LoginRequest(email, plainPassword);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<LoginResponse>;
            ok.Should().NotBeNull();
            var loginResponse = ok!.Value;
            loginResponse.Should().NotBeNull();

            var returnedUserObj = loginResponse.GetType().GetProperty("User")?.GetValue(loginResponse)
                                 ?? loginResponse.GetType().GetProperty("UserResponse")?.GetValue(loginResponse);

            returnedUserObj.Should().NotBeNull();
            var loyaltyPointsProp = returnedUserObj!.GetType().GetProperty("LoyaltyPoints");
            loyaltyPointsProp.Should().NotBeNull();
            var loyaltyPoints = (int?)loyaltyPointsProp!.GetValue(returnedUserObj);
            loyaltyPoints.Should().Be(100);
        }

        [Fact]
        public async Task Given_InvalidEmailFormat_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var request = new LoginRequest("not-an-email", "SomePassword123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");
        }
    }
}
