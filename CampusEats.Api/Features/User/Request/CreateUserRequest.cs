// Features/Users/CreateUserRequest.cs
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Users;

public record CreateUserRequest(string Name, string Email, UserRole Role);