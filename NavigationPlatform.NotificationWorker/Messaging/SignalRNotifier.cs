using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace NavigationPlatform.NotificationWorker.Messaging;

internal sealed class SignalRNotifier : ISignalRNotifier
{
    private readonly HubConnection _connection;
    private readonly ILogger<SignalRNotifier>? _logger;

    public SignalRNotifier(string hubUrl, ILogger<SignalRNotifier>? logger = null)
    {
        _logger = logger;
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.Closed += async (error) =>
        {
            _logger?.LogWarning("SignalR connection closed: {Error}", error?.Message);
            await Task.CompletedTask;
        };

        _connection.Reconnecting += (error) =>
        {
            _logger?.LogInformation("SignalR reconnecting: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            _logger?.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        try
        {
            _connection.StartAsync().GetAwaiter().GetResult();
            _logger?.LogInformation("SignalR connection started to {HubUrl}", hubUrl);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start SignalR connection to {HubUrl}", hubUrl);
            throw;
        }
    }

    public async Task NotifyAsync(Guid userId, string eventType, object payload)
    {
        try
        {
            if (_connection.State != HubConnectionState.Connected)
            {
                _logger?.LogWarning("SignalR not connected, current state: {State}", _connection.State);
                // Try to reconnect
                await _connection.StartAsync();
            }

            await _connection.InvokeAsync("NotifyUser", userId, eventType, payload);
            _logger?.LogDebug("Sent notification to user {UserId}, event: {EventType}", userId, eventType);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send notification to user {UserId}, event: {EventType}", userId, eventType);
            throw;
        }
    }
}
