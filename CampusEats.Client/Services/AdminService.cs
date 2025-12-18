using CampusEats.Client.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CampusEats.Client.Services;

public class AdminService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public AdminService(HttpClient http)
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
    /// Get paginated users with optional search and role filter
    /// </summary>
    public async Task<PaginatedUsersResponse?> GetUsersAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? role = null)
    {
        var url = $"/admin/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(role))
            url += $"&role={Uri.EscapeDataString(role)}";

        return await _http.GetFromJsonAsync<PaginatedUsersResponse>(url, _jsonOptions);
    }

    /// <summary>
    /// Get admin dashboard stats
    /// </summary>
    public async Task<AdminStatsResponse?> GetStatsAsync()
    {
        return await _http.GetFromJsonAsync<AdminStatsResponse>("/admin/stats", _jsonOptions);
    }

    /// <summary>
    /// Update user role (and optionally name/email)
    /// </summary>
    public async Task UpdateUserRoleAsync(Guid userId, string name, string email, string role)
    {
        var response = await _http.PutAsJsonAsync($"/users/{userId}", new { name, email, role }, _jsonOptions);
        await EnsureSuccessOrThrowApiException(response);
    }

    /// <summary>
    /// Delete a user
    /// </summary>
    public async Task DeleteUserAsync(Guid userId)
    {
        var response = await _http.DeleteAsync($"/users/{userId}");
        await EnsureSuccessOrThrowApiException(response);
    }
}
