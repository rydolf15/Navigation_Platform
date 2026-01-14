using NavigationPlatform.Domain.Common;
using NavigationPlatform.Domain.Journeys.Events;

namespace NavigationPlatform.Domain.Journeys;

public sealed class Journey : AggregateRoot
{
    public Guid UserId { get; private set; }
    public string StartLocation { get; private set; } = null!;
    public DateTime StartTime { get; private set; }
    public string ArrivalLocation { get; private set; } = null!;
    public DateTime ArrivalTime { get; private set; }
    public TransportType TransportType { get; private set; }
    public DistanceKm DistanceKm { get; private set; }
    public bool IsDailyGoalAchieved { get; private set; }

    private Journey() { }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            // We treat timezone-less values as UTC to avoid Npgsql failures when persisting to timestamptz.
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    public static Journey Create(
        Guid userId,
        string startLocation,
        DateTime startTime,
        string arrivalLocation,
        DateTime arrivalTime,
        TransportType transportType,
        DistanceKm distanceKm)
    {
        startTime = EnsureUtc(startTime);
        arrivalTime = EnsureUtc(arrivalTime);

        var journey = new Journey
        {
            UserId = userId,
            StartLocation = startLocation,
            StartTime = startTime,
            ArrivalLocation = arrivalLocation,
            ArrivalTime = arrivalTime,
            TransportType = transportType,
            DistanceKm = distanceKm
        };

        journey.Raise(new JourneyCreated(
            journey.Id,
            userId,
            journey.StartTime,
            journey.DistanceKm));
        return journey;
    }

    public void Update(
        string startLocation,
        DateTime startTime,
        string arrivalLocation,
        DateTime arrivalTime,
        TransportType transportType,
        DistanceKm distanceKm)
    {
        StartLocation = startLocation;
        StartTime = EnsureUtc(startTime);
        ArrivalLocation = arrivalLocation;
        ArrivalTime = EnsureUtc(arrivalTime);
        TransportType = transportType;
        DistanceKm = distanceKm;

        Raise(new JourneyUpdated(
            Id,
            UserId,
            StartTime,
            DistanceKm));
    }

    public void MarkDailyGoalAchieved()
    {
        if (IsDailyGoalAchieved) return;

        IsDailyGoalAchieved = true;
        Raise(new JourneyUpdated(
            Id,
            UserId,
            StartTime,
            DistanceKm));
    }

    public void Delete()
    {
        Raise(new JourneyDeleted(
            Id,
            UserId,
            StartTime,
            DistanceKm));
    }
}