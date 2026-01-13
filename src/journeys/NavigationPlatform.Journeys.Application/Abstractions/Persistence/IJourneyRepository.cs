using NavigationPlatform.Domain.Journeys;

namespace NavigationPlatform.Application.Abstractions.Persistence;

public interface IJourneyRepository
{
    Task AddAsync(Journey journey, CancellationToken ct);
    Task<Journey?> GetByIdAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Journey journey, CancellationToken ct);
}