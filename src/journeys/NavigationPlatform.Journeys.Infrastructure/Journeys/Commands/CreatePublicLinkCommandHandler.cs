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

public sealed class CreatePublicLinkCommandHandler
    : IRequestHandler<CreatePublicLinkCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreatePublicLinkCommandHandler(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreatePublicLinkCommand cmd, CancellationToken ct)
    {
        var journey = await _db.Journeys.FindAsync([cmd.JourneyId], ct)
            ?? throw new KeyNotFoundException("Journey not found.");

        // Only the owner can create/reuse a public link.
        if (journey.UserId != _currentUser.UserId)
            throw new UnauthorizedAccessException("User is not the owner of this journey.");

        // Idempotent: reuse existing active link for this journey.
        var existing = await _db.Set<JourneyPublicLink>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.JourneyId == cmd.JourneyId &&
                x.RevokedUtc == null, ct);

        if (existing != null)
            return existing.Id;

        var link = new JourneyPublicLink
        {
            JourneyId = cmd.JourneyId,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Add(link);

        _db.Add(new JourneyShareAudit
        {
            JourneyId = cmd.JourneyId,
            ActorUserId = _currentUser.UserId,
            Action = $"PUBLIC_LINK_CREATE:{link.Id}",
            OccurredUtc = DateTime.UtcNow
        });

        _db.OutboxMessages.Add(
            OutboxMessage.From(
                new JourneyShared(
                    journey.UserId,
                    cmd.JourneyId,
                    _currentUser.UserId,
                    SharedWithUserId: null,
                    PublicLinkId: link.Id)));

        await _db.SaveChangesAsync(ct);

        return link.Id;
    }
}

