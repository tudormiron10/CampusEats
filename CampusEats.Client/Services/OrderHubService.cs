using CampusEats.Client.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace CampusEats.Client.Services;

/// <summary>
/// Singleton service for managing SignalR connection to the order hub.
/// Provides real-time order status updates across all pages.
/// </summary>
public class OrderHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;
    private bool _isInitialized;
    private Func<Task<string?>>? _tokenProvider;

    /// <summary>
    /// Fired when any order's status changes.
    /// </summary>
    public event Action<OrderStatusUpdate>? OnOrderStatusChanged;

    /// <summary>
    /// Fired when a new order is placed (kitchen staff only).
    /// </summary>
    public event Action<NewOrderNotification>? OnNewOrder;

    /// <summary>
    /// Fired when an order is cancelled (kitchen staff only).
    /// </summary>
    public event Action<Guid>? OnOrderCancelled;

    /// <summary>
    /// Fired when connection state changes.
    /// </summary>
    public event Action<HubConnectionState>? OnConnectionStateChanged;

    /// <summary>
    /// Fired when connection is re-established after a disconnection.
    /// Components should refresh their data when this fires.
    /// </summary>
    public event Action? OnReconnected;

    /// <summary>
    /// Returns true if the hub connection is currently connected.
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Returns the current connection state.
    /// </summary>
    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// Creates the OrderHubService with the hub URL.
    /// </summary>
    /// <param name="hubUrl">The full URL to the SignalR hub (e.g., https://example.com/hubs/orders)</param>
    public OrderHubService(string hubUrl)
    {
        _hubUrl = hubUrl;
    }

    /// <summary>
    /// Starts the SignalR connection if not already started.
    /// Call this when the user is authenticated.
    /// </summary>
    /// <param name="tokenProvider">
    /// A delegate that provides the JWT access token. This follows Microsoft's
    /// SignalR AccessTokenProvider pattern - the delegate is called before every
    /// HTTP request made by SignalR, allowing token refresh if needed.
    /// </param>
    public async Task StartAsync(Func<Task<string?>> tokenProvider)
    {
        // Singleton pattern: only initialize once
        if (_isInitialized) return;

        // Store the token provider for use by AccessTokenProvider
        _tokenProvider = tokenProvider;

        // Verify we have a valid token before connecting
        var token = await tokenProvider();
        if (string.IsNullOrEmpty(token))
        {
            // Not logged in, don't connect
            return;
        }

        _isInitialized = true;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = _tokenProvider;
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        // Register event handlers (method names match IOrderHubClient interface)
        // Wrap in try-catch to prevent subscriber exceptions from crashing the hub connection
        _hubConnection.On<OrderStatusUpdate>("OrderStatusChanged", update =>
        {
            try
            {
                OnOrderStatusChanged?.Invoke(update);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in OrderStatusChanged handler: {ex.Message}");
            }
        });

        _hubConnection.On<NewOrderNotification>("NewOrder", order =>
        {
            try
            {
                OnNewOrder?.Invoke(order);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in NewOrder handler: {ex.Message}");
            }
        });

        _hubConnection.On<Guid>("OrderCancelled", orderId =>
        {
            try
            {
                OnOrderCancelled?.Invoke(orderId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in OrderCancelled handler: {ex.Message}");
            }
        });

        // Track connection state changes
        _hubConnection.Reconnecting += error =>
        {
            OnConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);
            // Notify subscribers to refresh their data after reconnection
            // Events missed during disconnection are lost, so refresh is important
            try
            {
                OnReconnected?.Invoke();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in Reconnected handler: {ex.Message}");
            }
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        }
        catch (Exception)
        {
            // Connection failed, reset state to allow retry
            _isInitialized = false;
            throw;
        }
    }

    /// <summary>
    /// Stops the SignalR connection.
    /// Call this when the user logs out.
    /// </summary>
    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            _isInitialized = false;
            OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// Disposes the SignalR connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}