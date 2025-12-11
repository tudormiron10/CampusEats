using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;

namespace CampusEats.Api.Tests.Features.User
{
    public class CreateUserHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private CampusEats.Api.Features.Users.CreateUserHandler _handler = null!;

        public CreateUserHandlerTests()
        {
            // each test class instance in xUnit is new per test, so this ensures a unique DB per test
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private CampusEats.Api.Features.Users.CreateUserHandler CreateSUT()
        {
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            return new CampusEats.Api.Features.Users.CreateUserHandler(_context);
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
        public async Task Given_ValidClientRequest_When_Handle_Then_ReturnsCreatedAndPersistsUserAndLoyaltyAndHashesPassword()
        {
            // Arrange
            var request = new CreateUserRequest("Client Name", "client@example.com", UserRole.Client, "Password123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Created");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            user.Should().NotBeNull();
            user!.Name.Should().Be(request.Name);
            user.Role.Should().Be(request.Role);

            var loyalty = await _context.Loyalties.SingleOrDefaultAsync(l => l.UserId == user.UserId);
            loyalty.Should().NotBeNull();
            loyalty!.CurrentPoints.Should().Be(0);

            // Password should be stored as hash (not equal to plain UTF8 bytes)
            user.PasswordHash.Should().NotBeNull();
            user.PasswordHash.Length.Should().BeGreaterThan(0);
            user.PasswordSalt.Should().NotBeNull();
            user.PasswordSalt.Length.Should().BeGreaterThan(0);
            user.PasswordHash.Should().NotEqual(Encoding.UTF8.GetBytes(request.Password));
        }

        [Fact]
        public async Task Given_ValidAdminRequest_When_Handle_Then_ReturnsCreatedAndPersistsUserWithoutLoyalty()
        {
            // Arrange
            var request = new CreateUserRequest("Admin Name", "admin@example.com", UserRole.Admin, "AdminPass123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Created");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            user.Should().NotBeNull();
            user!.Role.Should().Be(request.Role);

            var hasLoyalty = await _context.Loyalties.AnyAsync(l => l.UserId == user.UserId);
            hasLoyalty.Should().BeFalse();
        }

        [Fact]
        public async Task Given_ExistingEmail_When_Handle_Then_ReturnsConflict()
        {
            // Arrange
            var existingUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Existing",
                Email = "dup@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 1, 2, 3 },
                PasswordSalt = new byte[] { 4, 5, 6 }
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var request = new CreateUserRequest("New Name", "dup@example.com", UserRole.Client, "Password123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert - ApiErrors.EmailAlreadyExists() returns Conflict<ApiError>
            var conflict = result as Conflict<ApiError>;
            conflict.Should().NotBeNull();
            conflict!.Value!.Code.Should().Be("CONFLICT");

            var usersWithEmail = await _context.Users.CountAsync(u => u.Email == request.Email);
            usersWithEmail.Should().Be(1);
        }

        [Fact]
        public async Task Given_InvalidRequest_When_Handle_Then_ReturnsBadRequestAndDoesNotPersist()
        {
            // Arrange
            var request = new CreateUserRequest("", "invalid@example.com", UserRole.Client, "Password123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert - ApiErrors.ValidationFailed() returns BadRequest<ApiError>
            var badRequest = result as BadRequest<ApiError>;
            badRequest.Should().NotBeNull();
            badRequest!.Value!.Code.Should().Be("VALIDATION_FAILED");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            user.Should().BeNull();
        }

        [Fact]
        public async Task Given_LongName_When_Handle_Then_CreatedOrBadRequest_And_AppropriatePersistence()
        {
            // Arrange
            var longName = new string('A', 5000);
            var request = new CreateUserRequest(longName, "long@example.com", UserRole.Client, "Password123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            var typeName = result.GetType().Name;

            if (typeName.Contains("Created"))
            {
                // Created path assertions
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
                user.Should().NotBeNull();
                user!.Name.Should().Be(longName);

                // If role is Client, loyalty should also be created
                var loyalty = await _context.Loyalties.SingleOrDefaultAsync(l => l.UserId == user.UserId);
                loyalty.Should().NotBeNull();
                loyalty!.CurrentPoints.Should().Be(0);
            }
            else
            {
                // Expect a BadRequest (validation rejected the long name) and ensure nothing persisted
                typeName.Should().Contain("BadRequest");
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
                user.Should().BeNull();
            }
        }

        [Fact]
        public async Task Given_InvalidEmailFormat_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateUserRequest("Name", "invalid-email", UserRole.Client, "Password123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");

            var usersCount = await _context.Users.CountAsync();
            usersCount.Should().Be(0);
        }

        [Fact]
        public async Task Given_ShortPassword_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateUserRequest("Short Pass", "shortpass@example.com", UserRole.Client, "short");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            user.Should().BeNull();
        }

        [Fact]
        public async Task Given_ValidManagerRequest_When_Handle_Then_ReturnsCreatedAndPersistsUserWithoutLoyalty()
        {
            // Arrange
            var request = new CreateUserRequest("Manager Name", "manager@example.com", UserRole.Manager, "ManagerPass123");

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Created");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            user.Should().NotBeNull();
            user!.Role.Should().Be(request.Role);

            var hasLoyalty = await _context.Loyalties.AnyAsync(l => l.UserId == user.UserId);
            hasLoyalty.Should().BeFalse();
        }

        [Fact]
        public async Task Given_DuplicateCalls_When_Handle_Then_SecondCallReturnsConflictAndUserIsPersistedOnce()
        {
            // Arrange
            var request = new CreateUserRequest("Dup Name", "duplicate@example.com", UserRole.Client, "Password123");

            // Act
            var firstResult = await _handler.Handle(request, CancellationToken.None);
            var secondResult = await _handler.Handle(request, CancellationToken.None);

            // Assert
            firstResult.Should().NotBeNull();
            firstResult.GetType().Name.Should().Contain("Created");

            secondResult.Should().NotBeNull();
            secondResult.GetType().Name.Should().Contain("Conflict");

            var usersWithEmail = await _context.Users.CountAsync(u => u.Email == request.Email);
            usersWithEmail.Should().Be(1);
        }

        [Fact]
        public async Task Given_TwoUsersWithSamePassword_When_Handle_Then_HashesShouldBeDifferent()
        {
            // Arrange
            var password = "Password123!";
            var req1 = new CreateUserRequest("User One", "user1@example.com", UserRole.Client, password);
            var req2 = new CreateUserRequest("User Two", "user2@example.com", UserRole.Client, password);

            // Act
            var res1 = await _handler.Handle(req1, CancellationToken.None);
            var res2 = await _handler.Handle(req2, CancellationToken.None);

            // Assert results were created
            res1.Should().NotBeNull();
            res1.GetType().Name.Should().Contain("Created");
            res2.Should().NotBeNull();
            res2.GetType().Name.Should().Contain("Created");

            var user1 = await _context.Users.SingleOrDefaultAsync(u => u.Email == req1.Email);
            var user2 = await _context.Users.SingleOrDefaultAsync(u => u.Email == req2.Email);

            user1.Should().NotBeNull();
            user2.Should().NotBeNull();

            // Password hashes and salts should be unique per user
            user1!.PasswordHash.Should().NotBeNull();
            user2!.PasswordHash.Should().NotBeNull();
            user1.PasswordSalt.Should().NotBeNull();
            user2.PasswordSalt.Should().NotBeNull();

            // Ensure hash and salt are not equal between users
            user1.PasswordHash.Should().NotEqual(user2.PasswordHash);
            user1.PasswordSalt.Should().NotEqual(user2.PasswordSalt);
        }
    }
}
