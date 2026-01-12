namespace NavigationPlatform.Api.Realtime.Presence;

public interface IUserPresenceWriter
{
    Task SetOnlineAsync(Guid userId);
    Task SetOfflineAsync(Guid userId);
}
