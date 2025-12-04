using CampusEats.Client.Models;
using System.Net.Http.Json;

namespace CampusEats.Client.Services;

public class LoyaltyService
{
    private readonly HttpClient _http;

    public LoyaltyService(HttpClient http)
    {
        _http = http;
    }

    private async Task EnsureSuccessOrThrowApiException(HttpResponseMessage response)
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
        // TODO: Uncomment when backend is ready
        // return await _http.GetFromJsonAsync<LoyaltyStatusResponse>("/loyalty");

        await Task.CompletedTask; // Suppress async warning
        return null; // Empty state for now
    }

    /// <summary>
    /// Get the current user's loyalty transaction history
    /// </summary>
    public async Task<List<LoyaltyTransactionResponse>> GetTransactionsAsync()
    {
        // TODO: Uncomment when backend is ready
        // var response = await _http.GetFromJsonAsync<List<LoyaltyTransactionResponse>>("/loyalty/transactions");
        // return response ?? new List<LoyaltyTransactionResponse>();

        await Task.CompletedTask;
        return new List<LoyaltyTransactionResponse>();
    }

    /// <summary>
    /// Get available offers for the current user (filtered by their tier eligibility)
    /// </summary>
    public async Task<List<OfferResponse>> GetOffersAsync()
    {
        // TODO: Uncomment when backend is ready
        // var response = await _http.GetFromJsonAsync<List<OfferResponse>>("/loyalty/offers");
        // return response ?? new List<OfferResponse>();

        await Task.CompletedTask;
        return new List<OfferResponse>();
    }

    /// <summary>
    /// Redeem an offer - adds items to cart and marks for point payment
    /// </summary>
    public async Task RedeemOfferAsync(Guid offerId)
    {
        // TODO: Uncomment when backend is ready
        // var response = await _http.PostAsync($"/loyalty/offers/{offerId}/redeem", null);
        // await EnsureSuccessOrThrowApiException(response);

        await Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Manager endpoints
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get all offers (for management, includes inactive)
    /// </summary>
    public async Task<List<OfferResponse>> GetAllOffersAsync()
    {
        // TODO: Uncomment when backend is ready
        // var response = await _http.GetFromJsonAsync<List<OfferResponse>>("/loyalty/offers/manage");
        // return response ?? new List<OfferResponse>();

        await Task.CompletedTask;
        return new List<OfferResponse>();
    }

    /// <summary>
    /// Create a new offer
    /// </summary>
    public async Task<OfferResponse?> CreateOfferAsync(CreateOfferRequest request)
    {
        // TODO: Uncomment when backend is ready
        // var response = await _http.PostAsJsonAsync("/loyalty/offers", request);
        // await EnsureSuccessOrThrowApiException(response);
        // return await response.Content.ReadFromJsonAsync<OfferResponse>();

        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Update an existing offer
    /// </summary>
    public async Task<OfferResponse?> UpdateOfferAsync(Guid offerId, CreateOfferRequest request)
    {
        // TODO: Uncomment when backend is ready
        // var response = await _http.PutAsJsonAsync($"/loyalty/offers/{offerId}", request);
        // await EnsureSuccessOrThrowApiException(response);
        // return await response.Content.ReadFromJsonAsync<OfferResponse>();

        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Delete an offer
    /// </summary>
    public async Task DeleteOfferAsync(Guid offerId)
    {
        // TODO: Uncomment when backend is ready
        // var response = await _http.DeleteAsync($"/loyalty/offers/{offerId}");
        // await EnsureSuccessOrThrowApiException(response);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Toggle offer active status
    /// </summary>
    public async Task ToggleOfferStatusAsync(Guid offerId, bool isActive)
    {
        // TODO: Uncomment when backend is ready
        // var response = await _http.PatchAsJsonAsync($"/loyalty/offers/{offerId}/status", new { IsActive = isActive });
        // await EnsureSuccessOrThrowApiException(response);

        await Task.CompletedTask;
    }
}