using MediatR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Journeys.Dtos;
using NavigationPlatform.Application.Journeys.Queries;
using NavigationPlatform.Domain.Journeys;
using NavigationPlatform.Infrastructure.Persistence;

namespace NavigationPlatform.Infrastructure.Journeys.Queries;

public sealed class GetAdminJourneysQueryHandler
    : IRequestHandler<GetAdminJourneysQuery, AdminJourneysResult>
{
    private readonly AppDbContext _db;

    public GetAdminJourneysQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminJourneysResult> Handle(GetAdminJourneysQuery request, CancellationToken ct)
    {
        var query = _db.Journeys.AsNoTracking().AsQueryable();

        if (request.UserId.HasValue)
            query = query.Where(j => j.UserId == request.UserId.Value);

        if (!string.IsNullOrWhiteSpace(request.TransportType))
        {
            if (!Enum.TryParse<TransportType>(request.TransportType, ignoreCase: true, out var tt))
                throw new ArgumentException("Invalid TransportType.");

            query = query.Where(j => j.TransportType == tt);
        }

        if (request.StartDateFrom.HasValue)
            query = query.Where(j => j.StartTime >= request.StartDateFrom.Value);

        if (request.StartDateTo.HasValue)
            query = query.Where(j => j.StartTime <= request.StartDateTo.Value);

        if (request.ArrivalDateFrom.HasValue)
            query = query.Where(j => j.ArrivalTime >= request.ArrivalDateFrom.Value);

        if (request.ArrivalDateTo.HasValue)
            query = query.Where(j => j.ArrivalTime <= request.ArrivalDateTo.Value);

        if (request.MinDistance.HasValue)
            query = query.Where(j => (decimal)j.DistanceKm >= request.MinDistance.Value);

        if (request.MaxDistance.HasValue)
            query = query.Where(j => (decimal)j.DistanceKm <= request.MaxDistance.Value);

        var totalCount = await query.CountAsync(ct);

        var direction = request.Direction?.Trim();
        var asc = string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(direction);

        if (!asc && !desc)
            throw new ArgumentException("Invalid Direction. Use asc or desc.");

        var orderBy = request.OrderBy?.Trim();

        query = (orderBy?.ToLowerInvariant(), asc) switch
        {
            ("userid", true) => query.OrderBy(j => j.UserId),
            ("userid", false) => query.OrderByDescending(j => j.UserId),

            ("transporttype", true) => query.OrderBy(j => j.TransportType),
            ("transporttype", false) => query.OrderByDescending(j => j.TransportType),

            ("starttime", true) => query.OrderBy(j => j.StartTime),
            ("starttime", false) => query.OrderByDescending(j => j.StartTime),

            ("arrivaltime", true) => query.OrderBy(j => j.ArrivalTime),
            ("arrivaltime", false) => query.OrderByDescending(j => j.ArrivalTime),

            ("distancekm", true) => query.OrderBy(j => (decimal)j.DistanceKm),
            ("distancekm", false) => query.OrderByDescending(j => (decimal)j.DistanceKm),

            // default
            (null, _) => query.OrderByDescending(j => j.StartTime),
            ("", _) => query.OrderByDescending(j => j.StartTime),
            _ => throw new ArgumentException("Invalid OrderBy.")
        };

        var page = request.Page;
        var pageSize = request.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(j => new AdminJourneyDto(
                j.Id,
                j.UserId,
                j.StartLocation,
                j.StartTime,
                j.ArrivalLocation,
                j.ArrivalTime,
                j.TransportType.ToString(),
                (decimal)j.DistanceKm,
                j.IsDailyGoalAchieved))
            .ToListAsync(ct);

        return new AdminJourneysResult(items, totalCount, page, pageSize);
    }
}

