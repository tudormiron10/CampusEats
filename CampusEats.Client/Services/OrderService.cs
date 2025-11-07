using CampusEats.Client.Models;
using System.Net.Http.Json;

namespace CampusEats.Client.Services;

public class OrderService
{
    private readonly HttpClient _http;

    public OrderService(HttpClient http)
    {
        _http = http;
    }

    public async Task<OrderResponse?> CreateOrderAsync(CreateOrderRequest request)
    {
        var response = await _http.PostAsJsonAsync("/orders", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderResponse>();
    }

    public async Task<List<SimpleOrderResponse>> GetAllOrdersAsync()
    {
        var response = await _http.GetFromJsonAsync<List<SimpleOrderResponse>>("/orders");
        return response ?? new List<SimpleOrderResponse>();
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid orderId)
    {
        return await _http.GetFromJsonAsync<OrderResponse>($"/orders/{orderId}");
    }
}