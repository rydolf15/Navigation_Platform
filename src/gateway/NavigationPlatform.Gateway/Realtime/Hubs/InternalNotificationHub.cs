using Microsoft.AspNetCore.SignalR;

namespace NavigationPlatform.Gateway.Realtime.Hubs;

/// <summary>
/// Internal hub used ONLY by background services.
/// Not exposed to browsers.
/// </summary>
public sealed class InternalNotificationHub : Hub
{
    public async Task NotifyUser(
        Guid userId,
        string eventType,
        object payload)
    {
        await Clients.User(userId.ToString())
            .SendAsync(eventType, payload);
    }
}

