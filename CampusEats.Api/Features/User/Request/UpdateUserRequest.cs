// Features/Users/UpdateUserRequest.cs
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Users;

// Vom permite actualizarea numelui, email-ului și rolului
public record UpdateUserRequest(string Name, string Email, UserRole Role);