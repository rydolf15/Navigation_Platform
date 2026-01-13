namespace NavigationPlatform.Application.Abstractions.Messaging;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct);
}
