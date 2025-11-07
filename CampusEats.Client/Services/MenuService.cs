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
}