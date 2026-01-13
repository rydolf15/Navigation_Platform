using MediatR;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed record DeleteJourneyCommand(Guid JourneyId) : IRequest;