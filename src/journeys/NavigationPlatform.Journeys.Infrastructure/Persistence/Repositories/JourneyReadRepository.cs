using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Persistence;
using NavigationPlatform.Application.Common.Paging;
using NavigationPlatform.Application.Journeys.Dtos;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Sharing;
using System.Linq;

namespace NavigationPlatform.Infrastructure.Persistence.Journeys;

internal sealed class JourneyReadRepository : IJourneyReadRepository
{
    private readonly AppDbContext _db;

    public JourneyReadRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<JourneyDto>> GetPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = _db.Journeys
            .AsNoTracking()
            .Where(j =>
                j.UserId == userId ||
                _db.Set<JourneyShare>().Any(s =>
                    s.JourneyId == j.Id &&
                    s.SharedWithUserId == userId));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(j => j.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JourneyDto(
                j.Id,
                j.StartLocation,
                j.StartTime,
                j.ArrivalLocation,
                j.ArrivalTime,
                j.TransportType.ToString(),
                j.DistanceKm.Value,
                j.IsDailyGoalAchieved
            ))
            .ToListAsync(ct);

        return new PagedResult<JourneyDto>(
            items,
            page,
            pageSize,
            totalCount
        );
    }

    public async Task<JourneyDto?> GetByIdAsync(
        Guid journeyId,
        Guid userId,
        CancellationToken ct)
    {
        return await _db.Journeys
            .AsNoTracking()
            .Where(j =>
                j.Id == journeyId &&
                (j.UserId == userId ||
                 _db.Set<JourneyShare>().Any(s =>
                     s.JourneyId == j.Id &&
                     s.SharedWithUserId == userId)))
            .Select(j => new JourneyDto(
                j.Id,
                j.StartLocation,
                j.StartTime,
                j.ArrivalLocation,
                j.ArrivalTime,
                j.TransportType.ToString(),
                j.DistanceKm,
                j.IsDailyGoalAchieved
            ))
            .FirstOrDefaultAsync(ct);
    }
}
