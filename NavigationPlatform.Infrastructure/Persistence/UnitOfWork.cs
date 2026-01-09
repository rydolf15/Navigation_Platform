using NavigationPlatform.Application.Abstractions.Messaging;
using NavigationPlatform.Infrastructure.Persistence.Outbox;

namespace NavigationPlatform.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        foreach (var entry in _db.ChangeTracker.Entries())
        {
            if (entry.Entity is Domain.Common.AggregateRoot aggregate)
            {
                foreach (var evt in aggregate.DomainEvents)
                    await _db.OutboxMessages.AddAsync(
                        OutboxMessage.From(evt), ct);

                aggregate.ClearDomainEvents();
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}