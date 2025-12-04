using CampusEats.Client.Models;
using System.Net.Http.Json;

namespace CampusEats.Client.Services;

/// <summary>
/// Service for handling payment operations with the CampusEats API
/// </summary>
public class PaymentService
{
    private readonly HttpClient _http;

    public PaymentService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Creates a payment for an order and returns the Stripe ClientSecret
    /// </summary>
    /// <param name="orderId">The order ID to create payment for</param>
    /// <returns>PaymentResponse containing ClientSecret for Stripe.js</returns>
    public async Task<PaymentResponse?> CreatePaymentAsync(Guid orderId)
    {
        var request = new CreatePaymentRequest(orderId);
        var response = await _http.PostAsJsonAsync("/payments", request);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Payment creation failed: {error}", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PaymentResponse>();
    }

    /// <summary>
    /// Gets payment history for a specific user
    /// </summary>
    /// <param name="userId">The user ID to get payment history for</param>
    /// <returns>List of payment history records</returns>
    public async Task<List<PaymentHistoryResponse>> GetPaymentHistoryAsync(Guid userId)
    {
        var response = await _http.GetFromJsonAsync<List<PaymentHistoryResponse>>($"/payments/user/{userId}");
        return response ?? new List<PaymentHistoryResponse>();
    }
}

