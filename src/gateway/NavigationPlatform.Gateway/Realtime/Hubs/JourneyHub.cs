using Microsoft.AspNetCore.SignalR;
using NavigationPlatform.Gateway.Realtime.Presence;

namespace NavigationPlatform.Gateway.Realtime.Hubs;

public sealed class JourneyHub : Hub
{
    private readonly IUserPresenceWriter _presence;

    public JourneyHub(IUserPresenceWriter presence)
    {
        _presence = presence;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Guid.Parse(
            Context.User!.FindFirst("sub")!.Value);

        await _presence.SetOnlineAsync(userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Guid.Parse(
            Context.User!.FindFirst("sub")!.Value);

        await _presence.SetOfflineAsync(userId);
        await base.OnDisconnectedAsync(exception);
    }

    // Called by Notification service
    public Task NotifyUser(Guid userId, string eventType, object payload)
        => Clients.User(userId.ToString())
            .SendAsync(eventType, payload);
}

