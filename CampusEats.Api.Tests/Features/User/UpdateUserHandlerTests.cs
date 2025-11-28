using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;

namespace CampusEats.Api.Tests.Features.User
{
    public class UpdateUserHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private CampusEats.Api.Features.Users.UpdateUserHandler _handler = null!;

        public UpdateUserHandlerTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private CampusEats.Api.Features.Users.UpdateUserHandler CreateSUT()
        {
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            return new CampusEats.Api.Features.Users.UpdateUserHandler(_context);
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
        public async Task Given_ValidUpdate_When_Handle_Then_ReturnsOkAndPersistsChanges()
        {
            // Arrange
            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Original Name",
                Email = "original@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 }
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new UpdateUserRequest(user.UserId, "Updated Name", "updated@example.com", UserRole.Admin);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<UserResponse>;
            ok.Should().NotBeNull();
            var response = ok!.Value;
            response.UserId.Should().Be(user.UserId);
            response.Name.Should().Be("Updated Name");
            response.Email.Should().Be("updated@example.com");
            response.Role.Should().Be(UserRole.Admin.ToString());

            var persisted = await _context.Users.FindAsync(user.UserId);
            persisted.Should().NotBeNull();
            persisted!.Name.Should().Be("Updated Name");
            persisted.Email.Should().Be("updated@example.com");
            persisted.Role.Should().Be(UserRole.Admin);
        }

        [Fact]
        public async Task Given_NonExistentUser_When_Handle_Then_ReturnsNotFound()
        {
            // Arrange
            var randomId = Guid.NewGuid();
            var request = new UpdateUserRequest(randomId, "Name", "email@example.com", UserRole.Client);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var notFound = result as Microsoft.AspNetCore.Http.HttpResults.NotFound<string>;
            notFound.Should().NotBeNull();
            notFound!.Value.Should().Be("User not found.");
        }

        [Fact]
        public async Task Given_EmailTakenByAnotherUser_When_Handle_Then_ReturnsConflict()
        {
            // Arrange
            var userA = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "User A",
                Email = "a@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 }
            };
            var userB = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "User B",
                Email = "b@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 3 },
                PasswordSalt = new byte[] { 4 }
            };
            _context.Users.AddRange(userA, userB);
            await _context.SaveChangesAsync();

            var request = new UpdateUserRequest(userA.UserId, "User A Updated", userB.Email, UserRole.Client);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var conflict = result as Microsoft.AspNetCore.Http.HttpResults.Conflict<string>;
            conflict.Should().NotBeNull();
            conflict!.Value.Should().Be("An account with this email already exists.");
        }

        [Fact]
        public async Task Given_InvalidRequest_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "ToBeInvalid",
                Email = "tobeinvalid@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 5 },
                PasswordSalt = new byte[] { 6 }
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new UpdateUserRequest(user.UserId, "", "new@example.com", UserRole.Client);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");
        }

        [Fact]
        public async Task Given_RequestWithSameValuesAsExistingUser_When_Handle_Then_ReturnsOk_And_SkipsConflictCheck()
        {
            // Arrange
            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Same Name",
                Email = "same@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 }
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new UpdateUserRequest(user.UserId, user.Name, user.Email, user.Role);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<UserResponse>;
            ok.Should().NotBeNull();
            var response = ok!.Value;
            response.UserId.Should().Be(user.UserId);
            response.Name.Should().Be(user.Name);
            response.Email.Should().Be(user.Email);
            response.Role.Should().Be(user.Role.ToString());

            // Ensure no additional users were added and original remains unchanged
            var persisted = await _context.Users.FindAsync(user.UserId);
            persisted.Should().NotBeNull();
            persisted!.Name.Should().Be(user.Name);
            persisted.Email.Should().Be(user.Email);
            persisted.Role.Should().Be(user.Role);
        }

        [Fact]
        public async Task Given_InvalidEmailFormat_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Invalid Email User",
                Email = "valid@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 5 },
                PasswordSalt = new byte[] { 6 }
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new UpdateUserRequest(user.UserId, "New Name", "invalid-email-string", UserRole.Client);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");
        }

        [Fact]
        public async Task Given_WhitespaceOnlyName_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Original Name",
                Email = "whitespace@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 7 },
                PasswordSalt = new byte[] { 8 }
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new UpdateUserRequest(user.UserId, "   ", "new@example.com", UserRole.Client);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");
        }

        [Fact]
        public async Task Given_RoleChangeFromClientToManager_When_Handle_Then_PersistsNewRole()
        {
            // Arrange
            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Role Change User",
                Email = "rolechange@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 9 },
                PasswordSalt = new byte[] { 10 }
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new UpdateUserRequest(user.UserId, user.Name, user.Email, UserRole.Manager);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<UserResponse>;
            ok.Should().NotBeNull();
            var response = ok!.Value;
            response.Role.Should().Be(UserRole.Manager.ToString());

            var persisted = await _context.Users.FindAsync(user.UserId);
            persisted.Should().NotBeNull();
            persisted!.Role.Should().Be(UserRole.Manager);
        }

        [Fact]
        public async Task Given_NameExceeds100Chars_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var user = new Infrastructure.Persistence.Entities.User
            {
                UserId = Guid.NewGuid(),
                Name = "Original Name",
                Email = "namelimit@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 11 },
                PasswordSalt = new byte[] { 12 }
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var longName = new string('A', 101);
            var request = new UpdateUserRequest(user.UserId, longName, user.Email, user.Role);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");
        }
    }
}
