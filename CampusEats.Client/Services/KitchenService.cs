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
        var response = await _http.GetFromJsonAsync<List<KitchenOrderResponse>>("/api/kitchen/orders");
        return response ?? new List<KitchenOrderResponse>();
    }

    public async Task PrepareOrderAsync(Guid orderId)
    {
        await UpdateOrderStatusAsync(orderId, "InPreparation");
    }

    public async Task ReadyOrderAsync(Guid orderId)
    {
        await UpdateOrderStatusAsync(orderId, "Ready");
    }

    public async Task CompleteOrderAsync(Guid orderId)
    {
        await UpdateOrderStatusAsync(orderId, "Completed");
    }

    private async Task UpdateOrderStatusAsync(Guid orderId, string newStatus)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/kitchen/orders/{orderId}")
        {
            Content = JsonContent.Create(new { newStatus })
        };
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AnalyticsResponse?> GetAnalyticsAsync(DateTime startDate, DateTime endDate, string groupBy)
    {
        var startDateStr = Uri.EscapeDataString(startDate.ToString("o"));
        var endDateStr = Uri.EscapeDataString(endDate.ToString("o"));
        var url = $"/api/kitchen/analytics?startDate={startDateStr}&endDate={endDateStr}&groupBy={groupBy}";
        return await _http.GetFromJsonAsync<AnalyticsResponse>(url);
    }
}