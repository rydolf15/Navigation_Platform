using MediatR;

public sealed record FavoriteJourneyCommand(Guid JourneyId) : IRequest;
