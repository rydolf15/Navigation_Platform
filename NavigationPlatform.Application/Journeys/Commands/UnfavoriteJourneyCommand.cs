using MediatR;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed record UnfavoriteJourneyCommand(Guid JourneyId) : IRequest;
