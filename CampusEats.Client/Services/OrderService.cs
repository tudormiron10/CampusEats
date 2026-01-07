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
        // 1. Create Order
        var orderResponse = await _http.PostAsJsonAsync("/api/orders", request);
        orderResponse.EnsureSuccessStatusCode();
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        
        if (order == null)
            return null;

        // 2. Create Payment (Initiated)
        var paymentRequest = new { OrderId = order.OrderId };
        var paymentResponse = await _http.PostAsJsonAsync("/api/payments", paymentRequest);
        
        if (paymentResponse.IsSuccessStatusCode)
        {
            var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentResponse>();
            
            if (payment != null)
            {
                // 3. Confirm Payment (Successful) - This awards loyalty points
                var confirmRequest = new { PaymentId = payment.PaymentId, NewStatus = "Successful" };
                await _http.PostAsJsonAsync("/api/payments/confirmation", confirmRequest);
            }
        }

        return order;
    }

    public async Task<List<SimpleOrderResponse>> GetOrdersAsync()
    {
        // Backend returns user's orders (or all for staff) based on JWT
        var response = await _http.GetFromJsonAsync<List<SimpleOrderResponse>>("/api/orders");
        return response ?? new List<SimpleOrderResponse>();
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid orderId)
    {
        return await _http.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}");
    }

    public async Task<OrderResponse?> CancelOrderAsync(Guid orderId)
    {
        var response = await _http.DeleteAsync($"/api/orders/{orderId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderResponse>();
    }
}

// Payment DTOs for the order flow
public record PaymentResponse(Guid PaymentId, Guid OrderId, decimal Amount, string Status);
