using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace NavigationPlatform.Gateway.Realtime;

internal sealed class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? connection.User?.FindFirst("sub")?.Value;
}

