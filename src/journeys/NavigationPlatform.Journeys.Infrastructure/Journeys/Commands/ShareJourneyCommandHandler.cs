using MediatR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Journeys.Commands;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Audit;
using NavigationPlatform.Infrastructure.Persistence.Favourites;
using NavigationPlatform.Infrastructure.Persistence.Outbox;
using NavigationPlatform.Infrastructure.Persistence.Sharing;

namespace NavigationPlatform.Infrastructure.Journeys.Commands;

public sealed class ShareJourneyCommandHandler
    : IRequestHandler<ShareJourneyCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ShareJourneyCommandHandler(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(ShareJourneyCommand cmd, CancellationToken ct)
    {
        var journey = await _db.Journeys.FindAsync([cmd.JourneyId], ct)
            ?? throw new KeyNotFoundException("Journey not found.");

        // Only the owner can share.
        if (journey.UserId != _currentUser.UserId)
            throw new UnauthorizedAccessException("User is not the owner of this journey.");

        var desiredRecipients = (cmd.UserIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Where(x => x != _currentUser.UserId)
            .Distinct()
            .ToHashSet();

        // Set semantics: the posted list becomes the full recipient set.
        var existingShares = await _db.Set<JourneyShare>()
            .Where(x => x.JourneyId == cmd.JourneyId)
            .ToListAsync(ct);

        var existingRecipients = existingShares
            .Select(x => x.SharedWithUserId)
            .ToHashSet();

        var recipientsToAdd = desiredRecipients
            .Except(existingRecipients)
            .ToArray();

        var recipientsToRemove = existingRecipients
            .Except(desiredRecipients)
            .ToArray();

        foreach (var recipientUserId in recipientsToAdd)
        {
            _db.Add(new JourneyShare
            {
                JourneyId = cmd.JourneyId,
                SharedWithUserId = recipientUserId,
                SharedAtUtc = DateTime.UtcNow
            });

            _db.Add(new JourneyShareAudit
            {
                JourneyId = cmd.JourneyId,
                ActorUserId = _currentUser.UserId,
                Action = $"SHARE_USER:{recipientUserId}",
                OccurredUtc = DateTime.UtcNow
            });

            _db.OutboxMessages.Add(
                OutboxMessage.From(
                    new JourneyShared(
                        journey.UserId,
                        cmd.JourneyId,
                        _currentUser.UserId,
                        recipientUserId,
                        PublicLinkId: null)));
        }

        foreach (var recipientUserId in recipientsToRemove)
        {
            var share = existingShares.First(x => x.SharedWithUserId == recipientUserId);
            _db.Remove(share);

            _db.Add(new JourneyShareAudit
            {
                JourneyId = cmd.JourneyId,
                ActorUserId = _currentUser.UserId,
                Action = $"UNSHARE_USER:{recipientUserId}",
                OccurredUtc = DateTime.UtcNow
            });

            _db.OutboxMessages.Add(
                OutboxMessage.From(
                    new JourneyUnshared(
                        cmd.JourneyId,
                        _currentUser.UserId,
                        PublicLinkId: null,
                        UnsharedFromUserId: recipientUserId)));
        }

        // Optional but recommended: if a user is unshared, remove their favourite so they stop receiving updates.
        if (recipientsToRemove.Length > 0)
        {
            var removedFavourites = await _db.Set<JourneyFavourite>()
                .Where(x => x.JourneyId == cmd.JourneyId && recipientsToRemove.Contains(x.UserId))
                .ToListAsync(ct);

            if (removedFavourites.Count > 0)
            {
                _db.RemoveRange(removedFavourites);

                foreach (var fav in removedFavourites)
                {
                    _db.OutboxMessages.Add(
                        OutboxMessage.From(
                            new JourneyUnfavorited(cmd.JourneyId, fav.UserId)));
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

