using CampusEats.Client.Models;
using System.Net.Http.Json;

namespace CampusEats.Client.Services;

public class KitchenService
{
    private readonly HttpClient _http;

    public KitchenService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<KitchenOrderResponse>> GetKitchenOrdersAsync()
    {
        var response = await _http.GetFromJsonAsync<List<KitchenOrderResponse>>("/kitchen/orders");
        return response ?? new List<KitchenOrderResponse>();
    }

    public async Task PrepareOrderAsync(Guid orderId)
    {
        var response = await _http.PostAsync($"/kitchen/orders/{orderId}/prepare", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReadyOrderAsync(Guid orderId)
    {
        var response = await _http.PostAsync($"/kitchen/orders/{orderId}/ready", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteOrderAsync(Guid orderId)
    {
        var response = await _http.PostAsync($"/kitchen/orders/{orderId}/complete", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AnalyticsResponse?> GetAnalyticsAsync(DateTime startDate, DateTime endDate, string groupBy)
    {
        var startDateStr = Uri.EscapeDataString(startDate.ToString("o"));
        var endDateStr = Uri.EscapeDataString(endDate.ToString("o"));
        var url = $"/kitchen/analytics?startDate={startDateStr}&endDate={endDateStr}&groupBy={groupBy}";
        return await _http.GetFromJsonAsync<AnalyticsResponse>(url);
    }
}