using CampusEats.Client.Models;
using System.Net.Http.Json;

namespace CampusEats.Client.Services;

public class PaymentService
{
    private readonly HttpClient _http;

    public PaymentService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Initiates a checkout session with Stripe. Returns clientSecret for Payment Element.
    /// </summary>
    public async Task<InitiateCheckoutResult> InitiateCheckoutAsync(InitiateCheckoutRequest request)
    {
        var response = await _http.PostAsJsonAsync("/checkout", request);

        var result = new InitiateCheckoutResult();

        if (response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            // Free checkout - parse FreeCheckoutResponse
            var free = await response.Content.ReadFromJsonAsync<FreeCheckoutResponse>();
            result.Free = free;
            return result;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new ApiException(response.StatusCode, "CHECKOUT_ERROR", $"Failed to initiate checkout: {error}");
        }

        var session = await response.Content.ReadFromJsonAsync<CheckoutSessionResponse>();
        result.Session = session;
        return result;
    }

    /// <summary>
    /// Gets payment history for the current user.
    /// </summary>
    public async Task<List<PaymentHistoryItem>> GetPaymentHistoryAsync(Guid userId)
    {
        var response = await _http.GetFromJsonAsync<List<PaymentHistoryItem>>($"/payments/user/{userId}");
        return response ?? new List<PaymentHistoryItem>();
    }
}
