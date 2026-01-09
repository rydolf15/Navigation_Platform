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

    public static Journey Create(
        Guid userId,
        string startLocation,
        DateTime startTime,
        string arrivalLocation,
        DateTime arrivalTime,
        TransportType transportType,
        DistanceKm distanceKm)
    {
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

        journey.Raise(new JourneyCreated(journey.Id, userId));
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
        StartTime = startTime;
        ArrivalLocation = arrivalLocation;
        ArrivalTime = arrivalTime;
        TransportType = transportType;
        DistanceKm = distanceKm;

        Raise(new JourneyUpdated(Id, UserId));
    }

    public void MarkDailyGoalAchieved()
    {
        if (IsDailyGoalAchieved) return;

        IsDailyGoalAchieved = true;
        Raise(new JourneyUpdated(Id, UserId));
    }

    public void Delete()
    {
        Raise(new JourneyDeleted(Id, UserId));
    }
}