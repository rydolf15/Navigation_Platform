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
        // Snapshot tracked entities before adding OutboxMessages, otherwise the ChangeTracker
        // collection is modified during enumeration (EF adds new tracked outbox entities).
        var entries = _db.ChangeTracker.Entries().ToList();

        foreach (var entry in entries)
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