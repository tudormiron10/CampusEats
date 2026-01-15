using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Handler;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using System.Security.Cryptography;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;

namespace CampusEats.Api.Tests.Features.User;

public class ChangePasswordHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;
    private ChangePasswordHandler _handler = null!;

    public ChangePasswordHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _handler = CreateSUT();
    }

    private ChangePasswordHandler CreateSUT()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new ChangePasswordHandler(_context);
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

    #region Helper Methods

    private (byte[] hash, byte[] salt) CreatePasswordHash(string password)
    {
        using var hmac = new HMACSHA512();
        var salt = hmac.Key;
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return (hash, salt);
    }

    private async Task<UserEntity> SeedUser(string password = "CurrentPass123!")
    {
        var (hash, salt) = CreatePasswordHash(password);
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = $"test{Guid.NewGuid():N}@example.com",
            Role = UserRole.Client,
            PasswordHash = hash,
            PasswordSalt = salt
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_ValidRequest_When_ChangePassword_Then_ReturnsOk()
    {
        // Arrange
        var currentPassword = "CurrentPass123!";
        var user = await SeedUser(currentPassword);
        var request = new ChangePasswordRequest(
            user.UserId,
            currentPassword,
            "NewPassword456!"
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Given_ValidRequest_When_ChangePassword_Then_UpdatesPasswordHash()
    {
        // Arrange
        var currentPassword = "CurrentPass123!";
        var user = await SeedUser(currentPassword);
        var originalHash = user.PasswordHash.ToArray();
        var request = new ChangePasswordRequest(
            user.UserId,
            currentPassword,
            "NewPassword456!"
        );

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        var updatedUser = await _context.Users.FindAsync(user.UserId);
        updatedUser!.PasswordHash.Should().NotEqual(originalHash);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Given_NonExistentUser_When_ChangePassword_Then_ReturnsNotFound()
    {
        // Arrange
        var request = new ChangePasswordRequest(
            Guid.NewGuid(),
            "CurrentPass123!",
            "NewPassword456!"
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Given_IncorrectCurrentPassword_When_ChangePassword_Then_ReturnsUnauthorized()
    {
        // Arrange
        var user = await SeedUser("CurrentPass123!");
        var request = new ChangePasswordRequest(
            user.UserId,
            "WrongPassword!",
            "NewPassword456!"
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Given_EmptyNewPassword_When_ChangePassword_Then_ReturnsValidationError()
    {
        // Arrange
        var user = await SeedUser("CurrentPass123!");
        var request = new ChangePasswordRequest(
            user.UserId,
            "CurrentPass123!",
            ""
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Given_ShortNewPassword_When_ChangePassword_Then_ReturnsValidationError()
    {
        // Arrange
        var user = await SeedUser("CurrentPass123!");
        var request = new ChangePasswordRequest(
            user.UserId,
            "CurrentPass123!",
            "short"
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(400);
    }

    #endregion
}