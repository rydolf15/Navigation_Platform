namespace NavigationPlatform.Api.Realtime;

public interface IUserPresenceWriter
{
    Task SetOnlineAsync(Guid userId);
    Task SetOfflineAsync(Guid userId);
}
