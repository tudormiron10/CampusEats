using System.Net.Http.Json;
using Blazored.LocalStorage;
using CampusEats.Client.Models;

namespace CampusEats.Client.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;

    // LocalStorage key constants
    private const string AuthTokenKey = "authToken";
    private const string UserEmailKey = "userEmail";
    private const string UserFullNameKey = "userFullName";
    private const string UserRoleKey = "userRole";
    private const string UserIdKey = "userId";
    private const string LoyaltyPointsKey = "loyaltyPoints";

    public event Action? OnAuthStateChanged;

    public AuthService(HttpClient http, ILocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/users/login", request);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

                if (authResponse != null && authResponse.User != null)
                {
                    await _localStorage.SetItemAsync(AuthTokenKey, authResponse.Token);
                    await _localStorage.SetItemAsync(UserEmailKey, authResponse.User.Email);
                    await _localStorage.SetItemAsync(UserFullNameKey, authResponse.User.Name);
                    await _localStorage.SetItemAsync(UserRoleKey, authResponse.User.Role);
                    await _localStorage.SetItemAsync(UserIdKey, authResponse.User.UserId.ToString());
                    if (authResponse.User.LoyaltyPoints.HasValue)
                    {
                        await _localStorage.SetItemAsync(LoyaltyPointsKey, authResponse.User.LoyaltyPoints.Value.ToString());
                    }

                    OnAuthStateChanged?.Invoke();
                    return authResponse;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            // Create user via API
            var createUserRequest = new
            {
                name = request.Name,
                email = request.Email,
                password = request.Password,
                role = request.Role
            };

            var response = await _http.PostAsJsonAsync("/api/users", createUserRequest);

            if (response.IsSuccessStatusCode)
            {
                // After successful registration, automatically log in
                var loginRequest = new LoginRequest
                {
                    Email = request.Email,
                    Password = request.Password
                };

                return await LoginAsync(loginRequest);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(AuthTokenKey);
        await _localStorage.RemoveItemAsync(UserEmailKey);
        await _localStorage.RemoveItemAsync(UserFullNameKey);
        await _localStorage.RemoveItemAsync(UserRoleKey);
        await _localStorage.RemoveItemAsync(UserIdKey);
        await _localStorage.RemoveItemAsync(LoyaltyPointsKey);

        OnAuthStateChanged?.Invoke();
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _localStorage.GetItemAsync<string>(AuthTokenKey);
        return !string.IsNullOrEmpty(token);
    }

    public async Task<string?> GetUserEmailAsync()
    {
        return await _localStorage.GetItemAsync<string>(UserEmailKey);
    }

    public async Task<string?> GetUserFullNameAsync()
    {
        return await _localStorage.GetItemAsync<string>(UserFullNameKey);
    }

    public async Task<string?> GetUserRoleAsync()
    {
        return await _localStorage.GetItemAsync<string>(UserRoleKey);
    }

    public async Task<Guid?> GetUserIdAsync()
    {
        var userIdStr = await _localStorage.GetItemAsync<string>(UserIdKey);
        if (Guid.TryParse(userIdStr, out var userId))
        {
            return userId;
        }
        return null;
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(AuthTokenKey);
    }

    public async Task<int?> GetLoyaltyPointsAsync()
    {
        var pointsStr = await _localStorage.GetItemAsync<string>(LoyaltyPointsKey);
        if (int.TryParse(pointsStr, out var points))
        {
            return points;
        }
        return null;
    }

    public async Task UpdateUserInfoAsync(string name, string email)
    {
        await _localStorage.SetItemAsync(UserFullNameKey, name);
        await _localStorage.SetItemAsync(UserEmailKey, email);
        OnAuthStateChanged?.Invoke();
    }
}