using Microsoft.AspNetCore.SignalR.Client;

namespace NavigationPlatform.NotificationWorker.Messaging;

internal sealed class SignalRNotifier
{
    private readonly HubConnection _connection;

    public SignalRNotifier(string hubUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.StartAsync().GetAwaiter().GetResult();
    }

    public Task NotifyAsync(Guid userId, string eventType, object payload)
        => _connection.InvokeAsync("NotifyUser", userId, eventType, payload);
}
