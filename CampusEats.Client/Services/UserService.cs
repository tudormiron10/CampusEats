using CampusEats.Client.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CampusEats.Client.Services;

public class UserService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public UserService(HttpClient http)
    {
        _http = http;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    private async Task EnsureSuccessOrThrowApiException(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        ApiError? apiError = null;
        try
        {
            apiError = await response.Content.ReadFromJsonAsync<ApiError>();
        }
        catch
        {
            // If we can't parse the error, fall back to generic message
        }

        if (apiError != null)
        {
            throw new ApiException(response.StatusCode, apiError);
        }

        throw new ApiException(
            response.StatusCode,
            "ERROR",
            $"Request failed with status {(int)response.StatusCode} ({response.StatusCode})");
    }

    /// <summary>
    /// Get user profile by ID
    /// </summary>
    public async Task<UserProfileResponse?> GetProfileAsync(Guid userId)
    {
        return await _http.GetFromJsonAsync<UserProfileResponse>($"/api/users/{userId}", _jsonOptions);
    }

    /// <summary>
    /// Update user profile (name, email)
    /// </summary>
    public async Task<UserProfileResponse?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/users/{userId}", request, _jsonOptions);
        await EnsureSuccessOrThrowApiException(response);
        return await response.Content.ReadFromJsonAsync<UserProfileResponse>(_jsonOptions);
    }

    /// <summary>
    /// Change user password
    /// </summary>
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var response = await _http.PatchAsJsonAsync($"/api/users/{userId}/password", request, _jsonOptions);
        await EnsureSuccessOrThrowApiException(response);
    }
}