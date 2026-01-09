using Microsoft.AspNetCore.SignalR;

namespace NavigationPlatform.Api.Realtime;

internal sealed class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?
            .FindFirst("sub")?
            .Value;
}