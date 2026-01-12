using Microsoft.AspNetCore.SignalR;

namespace NavigationPlatform.Api.Realtime.Hubs;

/// <summary>
/// Internal hub used ONLY by background workers.
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
