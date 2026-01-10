using System.ComponentModel.DataAnnotations.Schema;

namespace NavigationPlatform.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    [NotMapped]
    private readonly List<DomainEvent> domainEvents = [];

    [NotMapped]
    public IReadOnlyCollection<DomainEvent> DomainEvents => domainEvents;

    protected void AddDomainEvent(DomainEvent domainEvent)
    {
        domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }
}
