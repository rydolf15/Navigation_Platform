namespace NavigationPlatform.Api.Contracts.Journeys;

public sealed record ShareJourneyRequest(
    IReadOnlyCollection<Guid> UserIds);
