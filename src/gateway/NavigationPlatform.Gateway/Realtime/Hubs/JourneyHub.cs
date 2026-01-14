using Microsoft.AspNetCore.SignalR;
using NavigationPlatform.Gateway.Realtime.Presence;
using Microsoft.Extensions.Logging;

namespace NavigationPlatform.Gateway.Realtime.Hubs;

public sealed class JourneyHub : Hub
{
    private readonly IUserPresenceWriter _presence;
    private readonly ILogger<JourneyHub> _logger;

    public JourneyHub(IUserPresenceWriter presence, ILogger<JourneyHub> logger)
    {
        _presence = presence;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("JourneyHub: OnConnectedAsync called, authenticated: {IsAuthenticated}", 
            Context.User?.Identity?.IsAuthenticated);

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            // Log all available claims for debugging
            var allClaims = Context.User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
            _logger.LogInformation("JourneyHub: All user claims: {Claims}", string.Join(", ", allClaims));

            // Try multiple ways to get the user ID
            var subClaim = Context.User.FindFirst("sub");
            var nameIdClaim = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var userIdClaim = subClaim ?? nameIdClaim;
            
            _logger.LogInformation("JourneyHub: sub claim: {Sub}, NameIdentifier claim: {NameId}", 
                subClaim?.Value, nameIdClaim?.Value);

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogInformation("JourneyHub: Setting user {UserId} as online", userId);
                await _presence.SetOnlineAsync(userId);
                _logger.LogInformation("JourneyHub: User {UserId} marked as online", userId);
            }
            else
            {
                _logger.LogWarning("JourneyHub: Could not parse userId. sub={Sub}, NameIdentifier={NameId}", 
                    subClaim?.Value, nameIdClaim?.Value);
            }
        }
        else
        {
            _logger.LogWarning("JourneyHub: User not authenticated in OnConnectedAsync");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "JourneyHub: Disconnected with exception");
        }

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            // Use same logic as OnConnectedAsync - try sub first, then NameIdentifier
            var subClaim = Context.User.FindFirst("sub");
            var nameIdClaim = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var userIdClaim = subClaim ?? nameIdClaim;
            
            _logger.LogInformation("JourneyHub: OnDisconnectedAsync - sub claim: {Sub}, NameIdentifier claim: {NameId}", 
                subClaim?.Value, nameIdClaim?.Value);

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogInformation("JourneyHub: Setting user {UserId} as offline", userId);
                await _presence.SetOfflineAsync(userId);
                _logger.LogInformation("JourneyHub: User {UserId} marked as offline", userId);
            }
            else
            {
                _logger.LogWarning("JourneyHub: Could not parse userId in OnDisconnectedAsync. sub={Sub}, NameIdentifier={NameId}", 
                    subClaim?.Value, nameIdClaim?.Value);
            }
        }
        else
        {
            _logger.LogWarning("JourneyHub: User not authenticated in OnDisconnectedAsync");
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Called by Notification service
    public Task NotifyUser(Guid userId, string eventType, object payload)
        => Clients.User(userId.ToString())
            .SendAsync(eventType, payload);
}

