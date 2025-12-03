using CampusEats.Client.Models;
using System.Net.Http.Json;

namespace CampusEats.Client.Services;

public class CategoryService
{
    private readonly HttpClient _http;

    public CategoryService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CategoryResponse>> GetCategoriesAsync()
    {
        var response = await _http.GetFromJsonAsync<List<CategoryResponse>>("/categories");
        return response ?? new List<CategoryResponse>();
    }

    public async Task<CategoryResponse?> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var response = await _http.PostAsJsonAsync("/categories", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CategoryResponse>();
    }

    public async Task<CategoryResponse?> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/categories/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CategoryResponse>();
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/categories/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task ReorderCategoriesAsync(List<Guid> orderedIds)
    {
        var request = new ReorderCategoriesRequest(orderedIds);
        var response = await _http.PatchAsJsonAsync("/categories/reorder", request);
        response.EnsureSuccessStatusCode();
    }
}
