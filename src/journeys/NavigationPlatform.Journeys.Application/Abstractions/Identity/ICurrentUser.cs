namespace NavigationPlatform.Application.Abstractions.Identity;

public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAdmin { get; }
    bool IsAuthenticated { get; }
}