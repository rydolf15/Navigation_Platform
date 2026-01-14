using MediatR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Abstractions.Messaging;
using NavigationPlatform.Application.Abstractions.Persistence;
using NavigationPlatform.Application.Journeys.Commands;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Audit;
using NavigationPlatform.Infrastructure.Persistence.Favourites;
using NavigationPlatform.Infrastructure.Persistence.Sharing;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed class DeleteJourneyCommandHandler
    : IRequestHandler<DeleteJourneyCommand>
{
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteJourneyCommandHandler(
        AppDbContext db,
        IUnitOfWork uow,
        ICurrentUser currentUser)
    {
        _db = db;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteJourneyCommand request, CancellationToken ct)
    {
        var journey = await _db.Journeys
            .FirstOrDefaultAsync(x => x.Id == request.JourneyId, ct)
            ?? throw new KeyNotFoundException("Journey not found.");

        // Check if user is owner OR shared recipient
        var isOwner = journey.UserId == _currentUser.UserId;
        var isSharedRecipient = !isOwner && await _db.Set<JourneyShare>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.JourneyId == request.JourneyId &&
                x.SharedWithUserId == _currentUser.UserId, ct);

        if (!isOwner && !isSharedRecipient)
            throw new UnauthorizedAccessException("User does not have permission to delete this journey.");

        // Delete all related entries before deleting the journey
        var favourites = await _db.Set<JourneyFavourite>()
            .Where(x => x.JourneyId == request.JourneyId)
            .ToListAsync(ct);
        _db.Set<JourneyFavourite>().RemoveRange(favourites);

        var shares = await _db.Set<JourneyShare>()
            .Where(x => x.JourneyId == request.JourneyId)
            .ToListAsync(ct);
        _db.Set<JourneyShare>().RemoveRange(shares);

        var publicLinks = await _db.Set<JourneyPublicLink>()
            .Where(x => x.JourneyId == request.JourneyId)
            .ToListAsync(ct);
        _db.Set<JourneyPublicLink>().RemoveRange(publicLinks);

        var audits = await _db.Set<JourneyShareAudit>()
            .Where(x => x.JourneyId == request.JourneyId)
            .ToListAsync(ct);
        _db.Set<JourneyShareAudit>().RemoveRange(audits);

        journey.Delete();
        _db.Journeys.Remove(journey);
        await _uow.CommitAsync(ct);
    }
}
