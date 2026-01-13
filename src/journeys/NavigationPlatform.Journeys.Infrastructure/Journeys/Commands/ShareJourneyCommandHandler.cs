using MediatR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Journeys.Commands;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Audit;
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
        if (cmd.UserIds == null || cmd.UserIds.Count == 0)
            return;

        var journey = await _db.Journeys.FindAsync([cmd.JourneyId], ct)
            ?? throw new KeyNotFoundException("Journey not found.");

        // Only the owner can share.
        if (journey.UserId != _currentUser.UserId)
            throw new UnauthorizedAccessException("User is not the owner of this journey.");

        var distinctRecipients = cmd.UserIds
            .Where(x => x != Guid.Empty)
            .Where(x => x != _currentUser.UserId)
            .Distinct()
            .ToArray();

        if (distinctRecipients.Length == 0)
            return;

        var existingRecipients = await _db.Set<JourneyShare>()
            .AsNoTracking()
            .Where(x => x.JourneyId == cmd.JourneyId && distinctRecipients.Contains(x.SharedWithUserId))
            .Select(x => x.SharedWithUserId)
            .ToListAsync(ct);

        var existingSet = existingRecipients.ToHashSet();

        foreach (var recipientUserId in distinctRecipients)
        {
            if (existingSet.Contains(recipientUserId))
                continue; // idempotent

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

        await _db.SaveChangesAsync(ct);
    }
}

