using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Handler;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.UserTests;

public class DeleteUserHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private DeleteUserHandler _handler = null!;

    public DeleteUserHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private DeleteUserHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new DeleteUserHandler(_context);
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

    #region Helper Methods

    private async Task<UserEntity> SeedUser(UserRole role = UserRole.Client)
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = $"test{Guid.NewGuid():N}@example.com",
            Role = role,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128]
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    private async Task<OrderEntity> SeedOrder(UserEntity user, OrderStatus status)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 10.00m
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return order;
    }

    private async Task<Loyalty> SeedLoyalty(UserEntity user)
    {
        var loyalty = new Loyalty
        {
            LoyaltyId = Guid.NewGuid(),
            UserId = user.UserId,
            CurrentPoints = 100,
            LifetimePoints = 500
        };

        _context.Loyalties.Add(loyalty);
        await _context.SaveChangesAsync();

        return loyalty;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ExistingUser_When_DeleteUser_Then_ReturnsNoContent()
    {
        // Arrange
        var adminUser = await SeedUser(UserRole.Admin);
        var targetUser = await SeedUser(UserRole.Client);
        var request = new DeleteUserRequest(targetUser.UserId, adminUser.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public async Task Given_ExistingUser_When_DeleteUser_Then_UserIsRemoved()
    {
        // Arrange
        var adminUser = await SeedUser(UserRole.Admin);
        var targetUser = await SeedUser(UserRole.Client);
        var request = new DeleteUserRequest(targetUser.UserId, adminUser.UserId);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var deletedUser = await _context.Users.FindAsync(targetUser.UserId);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task Given_UserWithLoyalty_When_DeleteUser_Then_LoyaltyIsRemoved()
    {
        // Arrange
        var adminUser = await SeedUser(UserRole.Admin);
        var targetUser = await SeedUser(UserRole.Client);
        var loyalty = await SeedLoyalty(targetUser);
        var request = new DeleteUserRequest(targetUser.UserId, adminUser.UserId);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var deletedLoyalty = await _context.Loyalties.FindAsync(loyalty.LoyaltyId);
        deletedLoyalty.Should().BeNull();
    }

    [Fact]
    public async Task Given_UserWithCompletedOrders_When_DeleteUser_Then_OrdersPreservedWithNullUserId()
    {
        // Arrange
        var adminUser = await SeedUser(UserRole.Admin);
        var targetUser = await SeedUser(UserRole.Client);
        var order = await SeedOrder(targetUser, OrderStatus.Completed);
        var request = new DeleteUserRequest(targetUser.UserId, adminUser.UserId);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var preservedOrder = await _context.Orders.FindAsync(order.OrderId);
        preservedOrder.Should().NotBeNull();
        preservedOrder!.UserId.Should().BeNull();
    }

    [Fact]
    public async Task Given_AdminDeletingAnotherAdmin_When_MultipleAdmins_Then_ReturnsNoContent()
    {
        // Arrange
        var adminUser1 = await SeedUser(UserRole.Admin);
        var adminUser2 = await SeedUser(UserRole.Admin);
        var request = new DeleteUserRequest(adminUser2.UserId, adminUser1.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContent>();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_NonExistentUser_When_DeleteUser_Then_ReturnsNotFound()
    {
        // Arrange
        var adminUser = await SeedUser(UserRole.Admin);
        var request = new DeleteUserRequest(Guid.NewGuid(), adminUser.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Given_SelfDeletion_When_DeleteUser_Then_ReturnsConflict()
    {
        // Arrange
        var user = await SeedUser(UserRole.Admin);
        var request = new DeleteUserRequest(user.UserId, user.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Given_UserWithActiveOrders_When_DeleteUser_Then_ReturnsConflict()
    {
        // Arrange
        var adminUser = await SeedUser(UserRole.Admin);
        var targetUser = await SeedUser(UserRole.Client);
        await SeedOrder(targetUser, OrderStatus.Pending);
        var request = new DeleteUserRequest(targetUser.UserId, adminUser.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Given_UserWithInPreparationOrder_When_DeleteUser_Then_ReturnsConflict()
    {
        // Arrange
        var adminUser = await SeedUser(UserRole.Admin);
        var targetUser = await SeedUser(UserRole.Client);
        await SeedOrder(targetUser, OrderStatus.InPreparation);
        var request = new DeleteUserRequest(targetUser.UserId, adminUser.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Given_LastAdmin_When_DeleteUser_Then_ReturnsConflict()
    {
        // Arrange
        var adminUser = await SeedUser(UserRole.Admin);
        var otherUser = await SeedUser(UserRole.Client);
        var request = new DeleteUserRequest(adminUser.UserId, otherUser.UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(409);
    }

    #endregion
}