using MediatR;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Abstractions.Messaging;
using NavigationPlatform.Application.Abstractions.Persistence;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed class DeleteJourneyCommandHandler
    : IRequestHandler<DeleteJourneyCommand>
{
    private readonly IJourneyRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteJourneyCommandHandler(
        IJourneyRepository repo,
        IUnitOfWork uow,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteJourneyCommand request, CancellationToken ct)
    {
        var journey = await _repo.GetByIdAsync(request.JourneyId, ct)
            ?? throw new KeyNotFoundException("Journey not found.");

        if (journey.UserId != _currentUser.UserId)
            throw new UnauthorizedAccessException("User is not the owner of this journey.");

        journey.Delete();
        await _repo.DeleteAsync(journey, ct);
        await _uow.CommitAsync(ct);
    }
}