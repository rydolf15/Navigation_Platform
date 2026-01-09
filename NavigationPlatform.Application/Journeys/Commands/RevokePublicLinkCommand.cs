using MediatR;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed record RevokePublicLinkCommand(Guid PublicLinkId)
    : IRequest;