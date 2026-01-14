using FluentAssertions;
using NavigationPlatform.Domain.Journeys;

namespace NavigationPlatform.Journeys.Domain.Tests;

public sealed class JourneyUtcNormalizationTests
{
    [Fact]
    public void Create_WhenStartAndArrivalAreUnspecified_ShouldStoreUtcKindWithoutChangingTicks()
    {
        var userId = Guid.NewGuid();

        var start = new DateTime(2026, 1, 14, 10, 41, 0, DateTimeKind.Unspecified);
        var arrival = new DateTime(2026, 1, 14, 12, 41, 0, DateTimeKind.Unspecified);

        var journey = Journey.Create(
            userId,
            startLocation: "a",
            startTime: start,
            arrivalLocation: "b",
            arrivalTime: arrival,
            transportType: TransportType.Car,
            distanceKm: new DistanceKm(21m));

        journey.StartTime.Kind.Should().Be(DateTimeKind.Utc);
        journey.StartTime.Ticks.Should().Be(start.Ticks);

        journey.ArrivalTime.Kind.Should().Be(DateTimeKind.Utc);
        journey.ArrivalTime.Ticks.Should().Be(arrival.Ticks);
    }

    [Fact]
    public void Create_WhenStartAndArrivalAreLocal_ShouldConvertToUtc()
    {
        var userId = Guid.NewGuid();

        var startLocal = new DateTime(2026, 1, 14, 10, 41, 0, DateTimeKind.Local);
        var arrivalLocal = new DateTime(2026, 1, 14, 12, 41, 0, DateTimeKind.Local);

        var journey = Journey.Create(
            userId,
            startLocation: "a",
            startTime: startLocal,
            arrivalLocation: "b",
            arrivalTime: arrivalLocal,
            transportType: TransportType.Car,
            distanceKm: new DistanceKm(21m));

        journey.StartTime.Should().Be(startLocal.ToUniversalTime());
        journey.StartTime.Kind.Should().Be(DateTimeKind.Utc);

        journey.ArrivalTime.Should().Be(arrivalLocal.ToUniversalTime());
        journey.ArrivalTime.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Update_WhenStartAndArrivalAreUnspecified_ShouldStoreUtcKind()
    {
        var userId = Guid.NewGuid();

        var journey = Journey.Create(
            userId,
            startLocation: "a",
            startTime: new DateTime(2026, 1, 14, 10, 41, 0, DateTimeKind.Utc),
            arrivalLocation: "b",
            arrivalTime: new DateTime(2026, 1, 14, 12, 41, 0, DateTimeKind.Utc),
            transportType: TransportType.Car,
            distanceKm: new DistanceKm(21m));

        var newStart = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Unspecified);
        var newArrival = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Unspecified);

        journey.Update(
            startLocation: "x",
            startTime: newStart,
            arrivalLocation: "y",
            arrivalTime: newArrival,
            transportType: TransportType.Train,
            distanceKm: new DistanceKm(10m));

        journey.StartTime.Kind.Should().Be(DateTimeKind.Utc);
        journey.StartTime.Ticks.Should().Be(newStart.Ticks);

        journey.ArrivalTime.Kind.Should().Be(DateTimeKind.Utc);
        journey.ArrivalTime.Ticks.Should().Be(newArrival.Ticks);
    }
}

