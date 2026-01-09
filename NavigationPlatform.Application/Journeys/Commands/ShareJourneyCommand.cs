using MediatR;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed record ShareJourneyCommand(
    Guid JourneyId,
    IReadOnlyCollection<Guid> UserIds)
    : IRequest;
