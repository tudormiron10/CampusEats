using CampusEats.Client.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CampusEats.Client.Services;

public class LoyaltyService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public LoyaltyService(HttpClient http)
    {
        _http = http;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    private static async Task EnsureSuccessOrThrowApiException(HttpResponseMessage response)
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

        throw new ApiException(
            response.StatusCode,
            "ERROR",
            $"Request failed with status {(int)response.StatusCode} ({response.StatusCode})");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Client endpoints
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get the current user's loyalty status (points, tier, progress)
    /// </summary>
    public async Task<LoyaltyStatusResponse?> GetStatusAsync()
    {
        return await _http.GetFromJsonAsync<LoyaltyStatusResponse>("/api/loyalty", _jsonOptions);
    }

    /// <summary>
    /// Get the current user's loyalty transaction history
    /// </summary>
    public async Task<List<LoyaltyTransactionResponse>> GetTransactionsAsync()
    {
        var response = await _http.GetFromJsonAsync<List<LoyaltyTransactionResponse>>("/api/loyalty/transactions", _jsonOptions);
        return response ?? new List<LoyaltyTransactionResponse>();
    }

    /// <summary>
    /// Get available offers for the current user (filtered by their tier eligibility)
    /// </summary>
    public async Task<List<OfferResponse>> GetOffersAsync()
    {
        var response = await _http.GetFromJsonAsync<List<OfferResponse>>("/api/loyalty/offers", _jsonOptions);
        return response ?? new List<OfferResponse>();
    }

    /// <summary>
    /// Redeem an offer - returns the redeemed items to add to cart
    /// </summary>
    public async Task<RedeemOfferResponse?> RedeemOfferAsync(Guid offerId)
    {
        var response = await _http.PostAsync($"/api/loyalty/offers/{offerId}/redeem", null);
        await EnsureSuccessOrThrowApiException(response);
        return await response.Content.ReadFromJsonAsync<RedeemOfferResponse>(_jsonOptions);
    }


    // ─────────────────────────────────────────────────────────────────────────────
    // Manager endpoints
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get all offers (for management, includes inactive)
    /// </summary>
    public async Task<List<OfferResponse>> GetAllOffersAsync()
    {
        var response = await _http.GetFromJsonAsync<List<OfferResponse>>("/api/loyalty/offers/manage", _jsonOptions);
        return response ?? new List<OfferResponse>();
    }

    /// <summary>
    /// Create a new offer
    /// </summary>
    public async Task<OfferResponse?> CreateOfferAsync(CreateOfferRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/loyalty/offers", request, _jsonOptions);
        await EnsureSuccessOrThrowApiException(response);
        return await response.Content.ReadFromJsonAsync<OfferResponse>(_jsonOptions);
    }

    /// <summary>
    /// Update an existing offer
    /// </summary>
    public async Task<OfferResponse?> UpdateOfferAsync(Guid offerId, CreateOfferRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/loyalty/offers/{offerId}", request, _jsonOptions);
        await EnsureSuccessOrThrowApiException(response);
        return await response.Content.ReadFromJsonAsync<OfferResponse>(_jsonOptions);
    }

    /// <summary>
    /// Delete an offer
    /// </summary>
    public async Task DeleteOfferAsync(Guid offerId)
    {
        var response = await _http.DeleteAsync($"/api/loyalty/offers/{offerId}");
        await EnsureSuccessOrThrowApiException(response);
    }

    /// <summary>
    /// Toggle offer active status
    /// </summary>
    public async Task ToggleOfferStatusAsync(Guid offerId, bool isActive)
    {
        var response = await _http.PatchAsJsonAsync($"/api/loyalty/offers/{offerId}/status", new { IsActive = isActive }, _jsonOptions);
        await EnsureSuccessOrThrowApiException(response);
    }
}