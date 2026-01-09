namespace NavigationPlatform.Domain.Common;

public abstract record DomainEvent(Guid Id, DateTime OccurredUtc);
