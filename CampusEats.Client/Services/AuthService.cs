using System.Net.Http.Json;
using Blazored.LocalStorage;
using CampusEats.Client.Models;

namespace CampusEats.Client.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;

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
            var response = await _http.PostAsJsonAsync("/users/login", request);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

                if (authResponse != null && authResponse.User != null)
                {
                    await _localStorage.SetItemAsync("authToken", authResponse.Token);
                    await _localStorage.SetItemAsync("userEmail", authResponse.User.Email);
                    await _localStorage.SetItemAsync("userFullName", authResponse.User.Name);
                    await _localStorage.SetItemAsync("userRole", authResponse.User.Role);
                    await _localStorage.SetItemAsync("userId", authResponse.User.UserId.ToString());
                    if (authResponse.User.LoyaltyPoints.HasValue)
                    {
                        await _localStorage.SetItemAsync("loyaltyPoints", authResponse.User.LoyaltyPoints.Value.ToString());
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

            var response = await _http.PostAsJsonAsync("/users", createUserRequest);

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
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("userEmail");
        await _localStorage.RemoveItemAsync("userFullName");
        await _localStorage.RemoveItemAsync("userRole");
        await _localStorage.RemoveItemAsync("userId");
        await _localStorage.RemoveItemAsync("loyaltyPoints");

        OnAuthStateChanged?.Invoke();
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        return !string.IsNullOrEmpty(token);
    }

    public async Task<string?> GetUserEmailAsync()
    {
        return await _localStorage.GetItemAsync<string>("userEmail");
    }

    public async Task<string?> GetUserFullNameAsync()
    {
        return await _localStorage.GetItemAsync<string>("userFullName");
    }

    public async Task<string?> GetUserRoleAsync()
    {
        return await _localStorage.GetItemAsync<string>("userRole");
    }

    public async Task<Guid?> GetUserIdAsync()
    {
        var userIdStr = await _localStorage.GetItemAsync<string>("userId");
        if (Guid.TryParse(userIdStr, out var userId))
        {
            return userId;
        }
        return null;
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>("authToken");
    }

    public async Task<int?> GetLoyaltyPointsAsync()
    {
        var pointsStr = await _localStorage.GetItemAsync<string>("loyaltyPoints");
        if (int.TryParse(pointsStr, out var points))
        {
            return points;
        }
        return null;
    }
}