using CampusEats.Client.Models;
using System.Net.Http.Json;

namespace CampusEats.Client.Services;

public class DietaryTagService
{
    private readonly HttpClient _http;

    public DietaryTagService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<DietaryTagResponse>> GetDietaryTagsAsync()
    {
        var response = await _http.GetFromJsonAsync<List<DietaryTagResponse>>("/dietary-tags");
        return response ?? new List<DietaryTagResponse>();
    }

    public async Task<DietaryTagResponse?> CreateDietaryTagAsync(CreateDietaryTagRequest request)
    {
        var response = await _http.PostAsJsonAsync("/dietary-tags", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DietaryTagResponse>();
    }

    public async Task<DietaryTagResponse?> UpdateDietaryTagAsync(Guid id, UpdateDietaryTagRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/dietary-tags/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DietaryTagResponse>();
    }

    public async Task DeleteDietaryTagAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/dietary-tags/{id}");
        response.EnsureSuccessStatusCode();
    }
}