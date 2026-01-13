using MediatR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Favourites;
using NavigationPlatform.Infrastructure.Persistence.Outbox;
using NavigationPlatform.Infrastructure.Persistence.Sharing;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed class UnfavoriteJourneyCommandHandler
    : IRequestHandler<UnfavoriteJourneyCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public UnfavoriteJourneyCommandHandler(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task Handle(UnfavoriteJourneyCommand cmd, CancellationToken ct)
    {
        var journey = await _db.Journeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cmd.JourneyId, ct);

        if (journey == null)
            throw new KeyNotFoundException("Journey not found.");

        // Per-resource access: owner or share recipient only.
        if (journey.UserId != _user.UserId)
        {
            var canAccess = await _db.Set<JourneyShare>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.JourneyId == cmd.JourneyId &&
                    x.SharedWithUserId == _user.UserId, ct);

            if (!canAccess)
                throw new KeyNotFoundException("Journey not found.");
        }

        var entity = await _db.Set<JourneyFavourite>()
            .FirstOrDefaultAsync(x =>
                x.JourneyId == cmd.JourneyId &&
                x.UserId == _user.UserId, ct);

        if (entity == null) return;

        _db.Remove(entity);

        _db.OutboxMessages.Add(
            OutboxMessage.From(
                new JourneyUnfavorited(cmd.JourneyId, _user.UserId)));

        await _db.SaveChangesAsync(ct);
    }
}
