using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.Users;
using CampusEats.Api.Features.User.Response;
using Microsoft.AspNetCore.Http.HttpResults;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;

namespace CampusEats.Api.Tests.Features.User
{
    public class GetAllUsersHandlerTests : IDisposable
    {
        private readonly string _dbName;
        private CampusEatsDbContext _context = null!;
        private GetAllUsersHandler _handler = null!;

        public GetAllUsersHandlerTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _handler = CreateSUT();
        }

        private GetAllUsersHandler CreateSUT()
        {
            var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new CampusEatsDbContext(options);
            _context.Database.EnsureCreated();

            return new GetAllUsersHandler(_context);
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
        public async Task Given_NoUsers_When_Handle_Then_ReturnsOkWithEmptyList()
        {
            // Arrange
            var request = new GetAllUserRequest();

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Ok");

            var valueProp = result.GetType().GetProperty("Value");
            valueProp.Should().NotBeNull();

            var valueObj = valueProp.GetValue(result);
            valueObj.Should().NotBeNull();

            // Cast the returned value to the expected strong type
            var users = valueObj as IEnumerable<UserResponse> ?? (valueObj as IEnumerable)?.Cast<UserResponse>();
            users.Should().NotBeNull();

            var items = users!.ToList();
            items.Should().BeEmpty();
        }

        [Fact]
        public async Task Given_UsersWithAndWithoutLoyalty_When_Handle_Then_ReturnsOkWithUsersAndPoints()
        {
            // Arrange
            var clientUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Client One",
                Email = "client1@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 }
            };

            var adminUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Admin One",
                Email = "admin1@example.com",
                Role = UserRole.Admin,
                PasswordHash = new byte[] { 3 },
                PasswordSalt = new byte[] { 4 }
            };

            var loyalty = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = clientUser.UserId,
                CurrentPoints = 10,
                LifetimePoints = 10
            };

            // Attach the loyalty to the user navigation so EF will populate it on Include
            clientUser.Loyalty = loyalty;

            _context.Users.AddRange(clientUser, adminUser);
            _context.Loyalties.Add(loyalty);
            await _context.SaveChangesAsync();

            var request = new GetAllUserRequest();

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Ok");

            var valueProp = result.GetType().GetProperty("Value");
            valueProp.Should().NotBeNull();

            var valueObj = valueProp.GetValue(result);
            valueObj.Should().NotBeNull();

            // Cast the returned value to the expected strong type
            var users = valueObj as IEnumerable<UserResponse> ?? (valueObj as IEnumerable)?.Cast<UserResponse>();
            users.Should().NotBeNull();

            var items = users!.ToList();
            items.Count.Should().Be(2);

            // find client item
            var clientItem = items.Single(i => i.Email == clientUser.Email);
            clientItem.Name.Should().Be(clientUser.Name);
            clientItem.Role.Should().Be(clientUser.Role.ToString());
            clientItem.LoyaltyPoints.Should().NotBeNull();
            clientItem.LoyaltyPoints.Should().Be(10);

            // find admin item
            var adminItem = items.Single(i => i.Email == adminUser.Email);
            adminItem.Name.Should().Be(adminUser.Name);
            adminItem.Role.Should().Be(adminUser.Role.ToString());
            adminItem.LoyaltyPoints.Should().BeNull();
        }

        [Fact]
        public async Task Given_MultipleClientsWithDifferentPoints_When_Handle_Then_ReturnsCorrectPointsForEachUser()
        {
            // Arrange
            var clientA = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Client A",
                Email = "clientA@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 }
            };

            var clientB = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Client B",
                Email = "clientB@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 3 },
                PasswordSalt = new byte[] { 4 }
            };

            var loyaltyA = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = clientA.UserId,
                CurrentPoints = 10,
                LifetimePoints = 10
            };

            var loyaltyB = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = clientB.UserId,
                CurrentPoints = 50,
                LifetimePoints = 50
            };

            clientA.Loyalty = loyaltyA;
            clientB.Loyalty = loyaltyB;

            _context.Users.AddRange(clientA, clientB);
            _context.Loyalties.AddRange(loyaltyA, loyaltyB);
            await _context.SaveChangesAsync();

            var request = new GetAllUserRequest();

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Ok");

            var value = result.GetType().GetProperty("Value")!.GetValue(result);
            var users = (value as IEnumerable<UserResponse>)!.ToList();

            var clientAItem = users.Single(u => u.Email == clientA.Email);
            clientAItem.LoyaltyPoints.Should().Be(10);

            var clientBItem = users.Single(u => u.Email == clientB.Email);
            clientBItem.LoyaltyPoints.Should().Be(50);
        }

        [Fact]
        public async Task Given_ClientWithoutLoyaltyAndManagerUser_When_Handle_Then_ReturnsNullPointsForBoth()
        {
            // Arrange
            var clientUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Client No Loyalty",
                Email = "clientnoloyalty@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 }
            };

            var managerUser = new UserEntity
            {
                UserId = Guid.NewGuid(),
                Name = "Manager User",
                Email = "manager@example.com",
                Role = UserRole.Manager,
                PasswordHash = new byte[] { 3 },
                PasswordSalt = new byte[] { 4 }
            };

            // Only add users; do NOT add any Loyalty rows
            _context.Users.AddRange(clientUser, managerUser);
            await _context.SaveChangesAsync();

            var request = new GetAllUserRequest();

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Ok");

            var value = result.GetType().GetProperty("Value")!.GetValue(result);
            var users = (value as IEnumerable<UserResponse>)!.ToList();

            var clientItem = users.Single(u => u.Email == clientUser.Email);
            clientItem.LoyaltyPoints.Should().BeNull();

            var managerItem = users.Single(u => u.Email == managerUser.Email);
            managerItem.LoyaltyPoints.Should().BeNull();
        }

        [Fact]
        public async Task Given_Users_When_Handle_Then_ReturnsUsersWithCorrectIds()
        {
            // Arrange
            var knownId = Guid.Parse("11111111-2222-3333-4444-555555555555");

            var user = new UserEntity
            {
                UserId = knownId,
                Name = "Known Id User",
                Email = "knownid@example.com",
                Role = UserRole.Client,
                PasswordHash = new byte[] { 9 },
                PasswordSalt = new byte[] { 8 }
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new GetAllUserRequest();

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Ok");

            var value = result.GetType().GetProperty("Value")!.GetValue(result);
            var users = (value as IEnumerable<UserResponse>)!.ToList();

            var item = users.Single(u => u.Email == user.Email);
            item.UserId.Should().Be(knownId);
        }
    }
}
