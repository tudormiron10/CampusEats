using CampusEats.Client.Models;
using System.Net.Http.Json;

namespace CampusEats.Client.Services;

public class MenuService
{
    private readonly HttpClient _http;

    public MenuService(HttpClient http)
    {
        _http = http;
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

        // Fallback for non-API errors
        throw new ApiException(
            response.StatusCode,
            "ERROR",
            $"Request failed with status {(int)response.StatusCode} ({response.StatusCode})");
    }

    public async Task<List<MenuItemResponse>> GetMenuAsync(string? category = null, string? dietaryKeyword = null)
    {
        var url = "/menu";
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(category))
            queryParams.Add($"category={Uri.EscapeDataString(category)}");

        if (!string.IsNullOrEmpty(dietaryKeyword))
            queryParams.Add($"dietaryKeyword={Uri.EscapeDataString(dietaryKeyword)}");

        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var response = await _http.GetFromJsonAsync<List<MenuItemResponse>>(url);
        return response ?? new List<MenuItemResponse>();
    }

    public async Task<MenuItemResponse?> GetMenuItemAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<MenuItemResponse>($"/menu/{id}");
    }

    public async Task<MenuItemResponse?> CreateMenuItemAsync(CreateMenuItemRequest request)
    {
        var response = await _http.PostAsJsonAsync("/menu", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MenuItemResponse>();
    }

    public async Task<MenuItemResponse?> UpdateMenuItemAsync(Guid id, UpdateMenuItemRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/menu/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MenuItemResponse>();
    }

    public async Task DeleteMenuItemAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/menu/{id}");
        await EnsureSuccessOrThrowApiException(response);
    }

    public async Task<string?> UploadImageAsync(Stream imageStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(streamContent, "file", fileName);

        var response = await _http.PostAsync("/upload/image", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
        return result?.Path;
    }
}

public record UploadImageResponse(string Path);