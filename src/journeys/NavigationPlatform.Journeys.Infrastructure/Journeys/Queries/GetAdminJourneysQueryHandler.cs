using MediatR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Journeys.Dtos;
using NavigationPlatform.Application.Journeys.Queries;
using NavigationPlatform.Domain.Journeys;
using NavigationPlatform.Infrastructure.Persistence;
using System.Globalization;

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

        if (!string.IsNullOrWhiteSpace(request.StartDateFrom) && TryParseDateTime(request.StartDateFrom, out var startDateFrom))
            query = query.Where(j => j.StartTime >= startDateFrom);

        if (!string.IsNullOrWhiteSpace(request.StartDateTo) && TryParseDateTime(request.StartDateTo, out var startDateTo))
            query = query.Where(j => j.StartTime <= startDateTo);

        if (!string.IsNullOrWhiteSpace(request.ArrivalDateFrom) && TryParseDateTime(request.ArrivalDateFrom, out var arrivalDateFrom))
            query = query.Where(j => j.ArrivalTime >= arrivalDateFrom);

        if (!string.IsNullOrWhiteSpace(request.ArrivalDateTo) && TryParseDateTime(request.ArrivalDateTo, out var arrivalDateTo))
            query = query.Where(j => j.ArrivalTime <= arrivalDateTo);

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

    private static bool TryParseDateTime(string value, out DateTime result)
    {
        // We always return a UTC DateTime to avoid Npgsql failures when querying timestamptz columns.
        // If the value contains a timezone indicator (Z or +/- offset), we parse it with that timezone.
        // Otherwise, we assume the value is in local time (from datetime-local input) and convert to UTC.

        // Check if the value has an explicit timezone indicator
        var hasExplicitTimezone = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || (value.Contains("+") && value.IndexOf('+') > 10) // + after the date part (e.g., "2026-01-14T18:52+01:00")
            || (value.Contains("-") && value.Length > 16 && value.LastIndexOf('-') > 10 && !value.EndsWith("-")); // - for timezone offset

        // Try common ISO 8601 formats including partial times
        var formats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss'Z'",
            "yyyy-MM-ddTHH:mm'Z'",
            "yyyy-MM-dd"
        };

        if (hasExplicitTimezone)
        {
            // Value has explicit timezone, parse with timezone support
            if (DateTimeOffset.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dto)
                || DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out dto))
            {
                result = dto.UtcDateTime;
                return true;
            }
        }
        else
        {
            // Value has no timezone info, assume it's local time (from datetime-local input) and convert to UTC
            if (DateTimeOffset.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var dtoLocal))
            {
                result = dtoLocal.ToUniversalTime().DateTime;
                return true;
            }

            // Fallback: try with DateTime
            if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var dt))
            {
                result = dt.ToUniversalTime();
                return true;
            }
        }

        result = default;
        return false;
    }
}

