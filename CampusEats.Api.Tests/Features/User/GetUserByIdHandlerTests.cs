using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;

namespace CampusEats.Api.Tests.Features.User
{
    public class GetUserByIdHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private CampusEats.Api.Features.Users.GetUserByIdHandler _handler = null!;

        public GetUserByIdHandlerTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private CampusEats.Api.Features.Users.GetUserByIdHandler CreateSUT()
        {
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            return new CampusEats.Api.Features.Users.GetUserByIdHandler(_context);
        }

        public void Dispose()
        {
            if (_context != null)
            {
                _context.Database.EnsureDeleted();
                _context.Dispose();
                _context = null!;
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Given_ExistingClientWithLoyalty_When_Handle_Then_ReturnsOkWithLoyaltyPoints()
        {
            // Arrange
            var clientUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Client Test",
                Email = "clienttest@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 }
            };

            var loyalty = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = clientUser.UserId,
                CurrentPoints = 50,
                LifetimePoints = 50
            };

            clientUser.Loyalty = loyalty;

            _context.Users.Add(clientUser);
            _context.Loyalties.Add(loyalty);
            await _context.SaveChangesAsync();

            var request = new GetUserByIdRequest(clientUser.UserId);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<UserResponse>;
            ok.Should().NotBeNull();
            ok!.Value.Should().NotBeNull();
            var response = ok.Value!;

            response.UserId.Should().Be(clientUser.UserId);
            response.Name.Should().Be(clientUser.Name);
            response.Email.Should().Be(clientUser.Email);
            response.Role.Should().Be(clientUser.Role.ToString());
            response.LoyaltyPoints.Should().Be(50);
        }

        [Fact]
        public async Task Given_ExistingAdminWithoutLoyalty_When_Handle_Then_ReturnsOkWithNullLoyaltyPoints()
        {
            // Arrange
            var adminUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Admin Test",
                Email = "admintest@example.com",
                Role = UserRole.Admin,
                PasswordHash = new byte[] { 3 },
                PasswordSalt = new byte[] { 4 }
            };

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            var request = new GetUserByIdRequest(adminUser.UserId);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<UserResponse>;
            ok.Should().NotBeNull();
            ok!.Value.Should().NotBeNull();
            var response = ok.Value!;

            response.UserId.Should().Be(adminUser.UserId);
            response.Name.Should().Be(adminUser.Name);
            response.Email.Should().Be(adminUser.Email);
            response.Role.Should().Be(adminUser.Role.ToString());
            response.LoyaltyPoints.Should().BeNull();
        }

        [Fact]
        public async Task Given_NonExistentUserId_When_Handle_Then_ReturnsNotFound()
        {
            // Arrange
            var randomId = Guid.NewGuid();
            var request = new GetUserByIdRequest(randomId);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert - ApiErrors.UserNotFound() returns NotFound<ApiError>
            var notFound = result as NotFound<ApiError>;
            notFound.Should().NotBeNull();
            notFound!.Value!.Code.Should().Be("NOT_FOUND");
            notFound.Value.Message.Should().Contain("User");
        }

        [Fact]
        public async Task Given_ClientUserWithoutLoyaltyRow_When_Handle_Then_ReturnsOkWithNullPoints()
        {
            // Arrange
            var clientUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Client No Loyalty",
                Email = "client.noloyalty@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 10 },
                PasswordSalt = new byte[] { 11 }
            };

            // Important: add only the user, no Loyalty row
            _context.Users.Add(clientUser);
            await _context.SaveChangesAsync();

            var request = new GetUserByIdRequest(clientUser.UserId);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<UserResponse>;
            ok.Should().NotBeNull();
            ok!.Value.Should().NotBeNull();

            var response = ok.Value!;
            response.UserId.Should().Be(clientUser.UserId);
            response.Role.Should().Be(clientUser.Role.ToString());
            response.LoyaltyPoints.Should().BeNull();
        }

        [Fact]
        public async Task Given_ManagerUser_When_Handle_Then_ReturnsOkWithNullPoints()
        {
            // Arrange
            var managerUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Manager User",
                Email = "manager@example.com",
                Role = UserRole.Manager,
                PasswordHash = new byte[] { 20 },
                PasswordSalt = new byte[] { 21 }
            };

            // Managers should not have loyalty accounts
            _context.Users.Add(managerUser);
            await _context.SaveChangesAsync();

            var request = new GetUserByIdRequest(managerUser.UserId);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<UserResponse>;
            ok.Should().NotBeNull();
            ok!.Value.Should().NotBeNull();

            var response = ok.Value!;
            response.UserId.Should().Be(managerUser.UserId);
            response.Role.Should().Be(managerUser.Role.ToString());
            response.LoyaltyPoints.Should().BeNull();
        }

        [Fact]
        public async Task Given_ClientWithZeroPoints_When_Handle_Then_ReturnsOkWithZeroValue()
        {
            // Arrange
            var clientUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Client Zero Points",
                Email = "client.zero@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 30 },
                PasswordSalt = new byte[] { 31 }
            };

            var loyalty = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = clientUser.UserId,
                CurrentPoints = 0,
                LifetimePoints = 0
            };

            clientUser.Loyalty = loyalty;

            _context.Users.Add(clientUser);
            _context.Loyalties.Add(loyalty);
            await _context.SaveChangesAsync();

            var request = new GetUserByIdRequest(clientUser.UserId);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var ok = result as Microsoft.AspNetCore.Http.HttpResults.Ok<UserResponse>;
            ok.Should().NotBeNull();
            ok!.Value.Should().NotBeNull();

            var response = ok.Value!;
            response.UserId.Should().Be(clientUser.UserId);
            response.Role.Should().Be(clientUser.Role.ToString());
            response.LoyaltyPoints.Should().NotBeNull();
            response.LoyaltyPoints.Should().Be(0);
        }
    }
}
