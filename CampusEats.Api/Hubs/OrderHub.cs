using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CampusEats.Api.Hubs;

/// <summary>
/// SignalR hub for real-time order notifications.
/// Uses strongly-typed IOrderHubClient for compile-time safety.
///
/// Groups:
/// - "user:{userId}" - Each authenticated user joins their personal group
/// - "kitchen" - Staff members (Manager/Admin) join to receive all order updates
/// </summary>
[Authorize]
public class OrderHub : Hub<IOrderHubClient>
{
    private readonly ILogger<OrderHub> _logger;

    public OrderHub(ILogger<OrderHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                // Every authenticated user joins their personal group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
                _logger.LogDebug("User {UserId} connected with ConnectionId {ConnectionId}", userId, Context.ConnectionId);

                // Staff members also join the kitchen group
                if (role is "Manager" or "Admin")
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "kitchen");
                    _logger.LogDebug("User {UserId} joined kitchen group", userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync for ConnectionId {ConnectionId}", Context.ConnectionId);
            // Exception is logged; allow base connection to proceed
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}