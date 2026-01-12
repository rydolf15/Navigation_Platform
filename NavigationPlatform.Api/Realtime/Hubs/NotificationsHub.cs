using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NavigationPlatform.Api.Realtime.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
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