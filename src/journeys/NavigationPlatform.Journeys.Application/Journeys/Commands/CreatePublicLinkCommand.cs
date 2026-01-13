using MediatR;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed record CreatePublicLinkCommand(Guid JourneyId)
: IRequest<Guid>;