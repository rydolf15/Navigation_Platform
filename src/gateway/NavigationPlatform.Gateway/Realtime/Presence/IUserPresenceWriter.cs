namespace NavigationPlatform.Gateway.Realtime.Presence;

public interface IUserPresenceWriter
{
    Task SetOnlineAsync(Guid userId);
    Task SetOfflineAsync(Guid userId);
}

