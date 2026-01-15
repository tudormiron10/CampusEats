using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Handler;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using UserEntity = CampusEats.Api.Infrastructure.Persistence.Entities.User;
using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Tests.Features.User;

public class GetUsersWithPaginationHandlerTests : IDisposable
{
    private readonly string _dbName;
    private CampusEatsDbContext _context = null!;

    public GetUsersWithPaginationHandlerTests()
    {
        _dbName = Guid.NewGuid().ToString();
    }

    private GetUsersWithPaginationHandler CreateSut()
    {
        var options = new DbContextOptionsBuilder<CampusEatsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new CampusEatsDbContext(options);
        _context.Database.EnsureCreated();

        return new GetUsersWithPaginationHandler(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private async Task<UserEntity> SeedUser(
        string name = "Test User",
        string? email = null,
        UserRole role = UserRole.Client,
        bool withLoyalty = false,
        int loyaltyPoints = 100,
        DateTime? createdAt = null)
    {
        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            Name = name,
            Email = email ?? $"test{Guid.NewGuid():N}@example.com",
            Role = role,
            PasswordHash = new byte[64],
            PasswordSalt = new byte[128],
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        if (withLoyalty)
        {
            user.Loyalty = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = user.UserId,
                CurrentPoints = loyaltyPoints,
                LifetimePoints = loyaltyPoints * 2
            };
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task SeedOrder(
        UserEntity user,
        OrderStatus status,
        DateTime? orderDate = null)
    {
        var order = new OrderEntity
        {
            OrderId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = status,
            TotalAmount = 25.00m,
            OrderDate = orderDate ?? DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Given_NoUsers_When_GetUsersWithPagination_Then_ReturnsEmptyList()
    {
        // Arrange
        var handler = CreateSut();
        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().BeEmpty();
        ok.Value.TotalCount.Should().Be(0);
        ok.Value.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task Given_UsersExist_When_GetUsersWithPagination_Then_ReturnsAllUsers()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice", "alice@test.com");
        await SeedUser("Bob", "bob@test.com");
        await SeedUser("Charlie", "charlie@test.com");

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(3);
        ok.Value.TotalCount.Should().Be(3);
        ok.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Given_UsersExist_When_GetUsersWithPagination_Then_UsersAreSortedByName()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Charlie");
        await SeedUser("Alice");
        await SeedUser("Bob");

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users[0].Name.Should().Be("Alice");
        ok.Value.Users[1].Name.Should().Be("Bob");
        ok.Value.Users[2].Name.Should().Be("Charlie");
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task Given_MoreUsersThanPageSize_When_GetUsersWithPagination_Then_ReturnsCorrectPage()
    {
        // Arrange
        var handler = CreateSut();
        for (int i = 1; i <= 15; i++)
        {
            await SeedUser($"User{i:D2}");
        }

        var request = new GetUsersWithPaginationRequest(2, 5, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(5);
        ok.Value.TotalCount.Should().Be(15);
        ok.Value.TotalPages.Should().Be(3);
        ok.Value.Page.Should().Be(2);
        ok.Value.PageSize.Should().Be(5);
        ok.Value.Users[0].Name.Should().Be("User06");
    }

    [Fact]
    public async Task Given_LastPage_When_GetUsersWithPagination_Then_ReturnsRemainingUsers()
    {
        // Arrange
        var handler = CreateSut();
        for (int i = 1; i <= 12; i++)
        {
            await SeedUser($"User{i:D2}");
        }

        var request = new GetUsersWithPaginationRequest(3, 5, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
        ok.Value.TotalCount.Should().Be(12);
        ok.Value.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Given_PageZero_When_GetUsersWithPagination_Then_TreatsAsPageOne()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice");
        await SeedUser("Bob");

        var request = new GetUsersWithPaginationRequest(0, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Page.Should().Be(1);
        ok.Value.Users.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_NegativePage_When_GetUsersWithPagination_Then_TreatsAsPageOne()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice");

        var request = new GetUsersWithPaginationRequest(-5, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Page.Should().Be(1);
    }

    [Fact]
    public async Task Given_PageSizeZero_When_GetUsersWithPagination_Then_ClampsToOne()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice");
        await SeedUser("Bob");

        var request = new GetUsersWithPaginationRequest(1, 0, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.PageSize.Should().Be(1);
        ok.Value.Users.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_PageSizeOver100_When_GetUsersWithPagination_Then_ClampsTo100()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice");

        var request = new GetUsersWithPaginationRequest(1, 500, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.PageSize.Should().Be(100);
    }

    #endregion

    #region Search Filter Tests

    [Fact]
    public async Task Given_SearchByName_When_GetUsersWithPagination_Then_FiltersCorrectly()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice Smith");
        await SeedUser("Bob Johnson");
        await SeedUser("Alice Johnson");

        var request = new GetUsersWithPaginationRequest(1, 10, "Alice", null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
        ok.Value.TotalCount.Should().Be(2);
        ok.Value.Users.Should().AllSatisfy(u => u.Name.Should().Contain("Alice"));
    }

    [Fact]
    public async Task Given_SearchByEmail_When_GetUsersWithPagination_Then_FiltersCorrectly()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice", "alice@company.com");
        await SeedUser("Bob", "bob@company.com");
        await SeedUser("Charlie", "charlie@other.com");

        var request = new GetUsersWithPaginationRequest(1, 10, "company.com", null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
        ok.Value.Users.Should().AllSatisfy(u => u.Email.Should().Contain("company.com"));
    }

    [Fact]
    public async Task Given_SearchCaseInsensitive_When_GetUsersWithPagination_Then_MatchesRegardlessOfCase()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("ALICE UPPERCASE");
        await SeedUser("alice lowercase");
        await SeedUser("Bob");

        var request = new GetUsersWithPaginationRequest(1, 10, "alice", null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_SearchNoMatches_When_GetUsersWithPagination_Then_ReturnsEmpty()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice");
        await SeedUser("Bob");

        var request = new GetUsersWithPaginationRequest(1, 10, "Zebra", null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().BeEmpty();
        ok.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptySearch_When_GetUsersWithPagination_Then_ReturnsAllUsers()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice");
        await SeedUser("Bob");

        var request = new GetUsersWithPaginationRequest(1, 10, "", null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_WhitespaceSearch_When_GetUsersWithPagination_Then_ReturnsAllUsers()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice");
        await SeedUser("Bob");

        var request = new GetUsersWithPaginationRequest(1, 10, "   ", null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
    }

    #endregion

    #region Role Filter Tests

    [Fact]
    public async Task Given_RoleFilterClient_When_GetUsersWithPagination_Then_ReturnsOnlyClients()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Client 1", role: UserRole.Client);
        await SeedUser("Client 2", role: UserRole.Client);
        await SeedUser("Admin", role: UserRole.Admin);
        await SeedUser("Manager", role: UserRole.Manager);

        var request = new GetUsersWithPaginationRequest(1, 10, null, "Client");

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
        ok.Value.Users.Should().AllSatisfy(u => u.Role.Should().Be("Client"));
    }

    [Fact]
    public async Task Given_RoleFilterAdmin_When_GetUsersWithPagination_Then_ReturnsOnlyAdmins()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Admin 1", role: UserRole.Admin);
        await SeedUser("Admin 2", role: UserRole.Admin);
        await SeedUser("Client", role: UserRole.Client);

        var request = new GetUsersWithPaginationRequest(1, 10, null, "Admin");

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
        ok.Value.Users.Should().AllSatisfy(u => u.Role.Should().Be("Admin"));
    }

    [Fact]
    public async Task Given_RoleFilterManager_When_GetUsersWithPagination_Then_ReturnsOnlyManagers()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Manager 1", role: UserRole.Manager);
        await SeedUser("Client", role: UserRole.Client);

        var request = new GetUsersWithPaginationRequest(1, 10, null, "Manager");

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].Role.Should().Be("Manager");
    }

    [Fact]
    public async Task Given_RoleFilterCaseInsensitive_When_GetUsersWithPagination_Then_MatchesCorrectly()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Admin", role: UserRole.Admin);
        await SeedUser("Client", role: UserRole.Client);

        var request = new GetUsersWithPaginationRequest(1, 10, null, "admin");

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Given_InvalidRoleFilter_When_GetUsersWithPagination_Then_ReturnsAllUsers()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Admin", role: UserRole.Admin);
        await SeedUser("Client", role: UserRole.Client);

        var request = new GetUsersWithPaginationRequest(1, 10, null, "InvalidRole");

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_EmptyRoleFilter_When_GetUsersWithPagination_Then_ReturnsAllUsers()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Admin", role: UserRole.Admin);
        await SeedUser("Client", role: UserRole.Client);

        var request = new GetUsersWithPaginationRequest(1, 10, null, "");

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(2);
    }

    #endregion

    #region Combined Filters Tests

    [Fact]
    public async Task Given_SearchAndRoleFilter_When_GetUsersWithPagination_Then_AppliesBothFilters()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Alice Admin", role: UserRole.Admin);
        await SeedUser("Alice Client", role: UserRole.Client);
        await SeedUser("Bob Admin", role: UserRole.Admin);
        await SeedUser("Bob Client", role: UserRole.Client);

        var request = new GetUsersWithPaginationRequest(1, 10, "Alice", "Admin");

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].Name.Should().Be("Alice Admin");
        ok.Value.Users[0].Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Given_SearchRoleAndPagination_When_GetUsersWithPagination_Then_AppliesAllCorrectly()
    {
        // Arrange
        var handler = CreateSut();
        for (int i = 1; i <= 10; i++)
        {
            await SeedUser($"Client{i:D2}", role: UserRole.Client);
        }
        await SeedUser("Admin User", role: UserRole.Admin);

        var request = new GetUsersWithPaginationRequest(2, 3, "Client", "Client");

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(3);
        ok.Value.TotalCount.Should().Be(10);
        ok.Value.TotalPages.Should().Be(4);
        ok.Value.Page.Should().Be(2);
    }

    #endregion

    #region User Response Fields Tests

    [Fact]
    public async Task Given_UserWithLoyalty_When_GetUsersWithPagination_Then_ReturnsLoyaltyPoints()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Loyal User", withLoyalty: true, loyaltyPoints: 500);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].LoyaltyPoints.Should().Be(500);
    }

    [Fact]
    public async Task Given_UserWithoutLoyalty_When_GetUsersWithPagination_Then_LoyaltyPointsIsNull()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("Regular User", withLoyalty: false);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users[0].LoyaltyPoints.Should().BeNull();
    }

    [Fact]
    public async Task Given_UserWithOrders_When_GetUsersWithPagination_Then_ReturnsTotalOrders()
    {
        // Arrange
        var handler = CreateSut();
        var user = await SeedUser("Order User");
        await SeedOrder(user, OrderStatus.Completed);
        await SeedOrder(user, OrderStatus.Completed);
        await SeedOrder(user, OrderStatus.Pending);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].TotalOrders.Should().Be(3);
    }

    [Fact]
    public async Task Given_UserWithNoOrders_When_GetUsersWithPagination_Then_TotalOrdersIsZero()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("No Orders User");

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].TotalOrders.Should().Be(0);
    }

    [Fact]
    public async Task Given_UserWithOrders_When_GetUsersWithPagination_Then_ReturnsLastOrderDate()
    {
        // Arrange
        var handler = CreateSut();
        var user = await SeedUser("Active User");
        var oldDate = DateTime.UtcNow.AddDays(-30);
        var recentDate = DateTime.UtcNow.AddDays(-1);
        
        await SeedOrder(user, OrderStatus.Completed, oldDate);
        await SeedOrder(user, OrderStatus.Completed, recentDate);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].LastOrderDate.Should().BeCloseTo(recentDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Given_UserWithNoOrders_When_GetUsersWithPagination_Then_LastOrderDateIsNull()
    {
        // Arrange
        var handler = CreateSut();
        await SeedUser("New User");

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].LastOrderDate.Should().BeNull();
    }

    #endregion

    #region Active Orders Tests

    [Fact]
    public async Task Given_UserWithPendingOrder_When_GetUsersWithPagination_Then_HasActiveOrdersIsTrue()
    {
        // Arrange
        var handler = CreateSut();
        var user = await SeedUser("Pending User");
        await SeedOrder(user, OrderStatus.Pending);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].HasActiveOrders.Should().BeTrue();
    }

    [Fact]
    public async Task Given_UserWithInPreparationOrder_When_GetUsersWithPagination_Then_HasActiveOrdersIsTrue()
    {
        // Arrange
        var handler = CreateSut();
        var user = await SeedUser("InPrep User");
        await SeedOrder(user, OrderStatus.InPreparation);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].HasActiveOrders.Should().BeTrue();
    }

    [Fact]
    public async Task Given_UserWithReadyOrder_When_GetUsersWithPagination_Then_HasActiveOrdersIsTrue()
    {
        // Arrange
        var handler = CreateSut();
        var user = await SeedUser("Ready User");
        await SeedOrder(user, OrderStatus.Ready);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].HasActiveOrders.Should().BeTrue();
    }

    [Fact]
    public async Task Given_UserWithOnlyCompletedOrders_When_GetUsersWithPagination_Then_HasActiveOrdersIsFalse()
    {
        // Arrange
        var handler = CreateSut();
        var user = await SeedUser("Completed User");
        await SeedOrder(user, OrderStatus.Completed);
        await SeedOrder(user, OrderStatus.Completed);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].HasActiveOrders.Should().BeFalse();
    }

    [Fact]
    public async Task Given_UserWithOnlyCancelledOrders_When_GetUsersWithPagination_Then_HasActiveOrdersIsFalse()
    {
        // Arrange
        var handler = CreateSut();
        var user = await SeedUser("Cancelled User");
        await SeedOrder(user, OrderStatus.Cancelled);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].HasActiveOrders.Should().BeFalse();
    }

    [Fact]
    public async Task Given_UserWithMixedOrderStatuses_When_GetUsersWithPagination_Then_HasActiveOrdersIsTrue()
    {
        // Arrange
        var handler = CreateSut();
        var user = await SeedUser("Mixed User");
        await SeedOrder(user, OrderStatus.Completed);
        await SeedOrder(user, OrderStatus.Cancelled);
        await SeedOrder(user, OrderStatus.Pending);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        ok.Value!.Users.Should().HaveCount(1);
        ok.Value.Users[0].HasActiveOrders.Should().BeTrue();
    }

    #endregion

    #region Response Structure Tests

    [Fact]
    public async Task Given_Users_When_GetUsersWithPagination_Then_ReturnsCorrectResponseStructure()
    {
        // Arrange
        var handler = CreateSut();
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var user = await SeedUser("Full User", "fulluser@test.com", UserRole.Client, true, 250, createdAt);
        await SeedOrder(user, OrderStatus.Completed);

        var request = new GetUsersWithPaginationRequest(1, 10, null, null);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        var ok = result as Ok<PaginatedUsersResponse>;
        ok.Should().NotBeNull();
        ok!.Value.Should().NotBeNull();
        
        var response = ok.Value!;
        response.TotalCount.Should().Be(1);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(10);
        response.TotalPages.Should().Be(1);
        
        var userResponse = response.Users[0];
        userResponse.UserId.Should().Be(user.UserId);
        userResponse.Name.Should().Be("Full User");
        userResponse.Email.Should().Be("fulluser@test.com");
        userResponse.Role.Should().Be("Client");
        userResponse.LoyaltyPoints.Should().Be(250);
        userResponse.CreatedAt.Should().BeCloseTo(createdAt, TimeSpan.FromSeconds(1));
        userResponse.TotalOrders.Should().Be(1);
    }

    #endregion
}

