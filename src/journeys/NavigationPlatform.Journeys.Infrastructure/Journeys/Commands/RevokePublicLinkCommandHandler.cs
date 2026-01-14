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

public sealed class RevokePublicLinkCommandHandler
    : IRequestHandler<RevokePublicLinkCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RevokePublicLinkCommandHandler(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(RevokePublicLinkCommand cmd, CancellationToken ct)
    {
        var link = await _db.Set<JourneyPublicLink>()
            .FirstOrDefaultAsync(x => x.Id == cmd.PublicLinkId, ct)
            ?? throw new KeyNotFoundException("Public link not found.");

        var journey = await _db.Journeys.FindAsync([link.JourneyId], ct)
            ?? throw new KeyNotFoundException("Journey not found.");

        // Only the owner can revoke.
        if (journey.UserId != _currentUser.UserId)
            throw new UnauthorizedAccessException("User is not the owner of this journey.");

        // Idempotent revoke.
        if (link.RevokedUtc != null)
            return;

        link.RevokedUtc = DateTime.UtcNow;

        _db.Add(new JourneyShareAudit
        {
            JourneyId = link.JourneyId,
            ActorUserId = _currentUser.UserId,
            Action = $"PUBLIC_LINK_REVOKE:{link.Id}",
            OccurredUtc = DateTime.UtcNow
        });

        // Optional: publish a domain event for downstream services.
        _db.OutboxMessages.Add(
            OutboxMessage.From(
                new JourneyUnshared(
                    link.JourneyId,
                    _currentUser.UserId,
                    PublicLinkId: link.Id,
                    UnsharedFromUserId: null)));

        await _db.SaveChangesAsync(ct);
    }
}

