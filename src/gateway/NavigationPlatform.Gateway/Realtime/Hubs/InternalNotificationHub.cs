using Microsoft.AspNetCore.SignalR;

namespace NavigationPlatform.Gateway.Realtime.Hubs;

/// <summary>
/// Internal hub used ONLY by background services.
/// Not exposed to browsers.
/// </summary>
public sealed class InternalNotificationHub : Hub
{
    private readonly IHubContext<NotificationsHub> _notificationsHub;

    public InternalNotificationHub(IHubContext<NotificationsHub> notificationsHub)
    {
        _notificationsHub = notificationsHub;
    }

    public async Task NotifyUser(
        Guid userId,
        string eventType,
        object payload)
    {
        // Relay to browser-connected hub(s). Background services connect only to this internal hub.
        await _notificationsHub.Clients.User(userId.ToString())
            .SendAsync(eventType, payload);
    }
}

