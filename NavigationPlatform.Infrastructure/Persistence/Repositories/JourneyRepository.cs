using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Persistence;
using NavigationPlatform.Domain.Journeys;

namespace NavigationPlatform.Infrastructure.Persistence.Repositories;

internal sealed class JourneyRepository : IJourneyRepository
{
    private readonly AppDbContext _db;

    public JourneyRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Journey journey, CancellationToken ct)
        => await _db.Journeys.AddAsync(journey, ct);

    public async Task<Journey?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Journeys.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task DeleteAsync(Journey journey, CancellationToken ct)
    {
        _db.Journeys.Remove(journey);
        return Task.CompletedTask;
    }
}