using MediatR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Favourites;
using NavigationPlatform.Infrastructure.Persistence.Outbox;

public sealed class FavoriteJourneyCommandHandler
    : IRequestHandler<FavoriteJourneyCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public FavoriteJourneyCommandHandler(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task Handle(FavoriteJourneyCommand cmd, CancellationToken ct)
    {
        var exists = await _db.Set<JourneyFavourite>()
            .AnyAsync(x =>
                x.JourneyId == cmd.JourneyId &&
                x.UserId == _user.UserId, ct);

        if (exists) return;

        _db.Add(new JourneyFavourite
        {
            JourneyId = cmd.JourneyId,
            UserId = _user.UserId
        });

        _db.OutboxMessages.Add(
            OutboxMessage.From(
                new JourneyFavorited(cmd.JourneyId, _user.UserId)));

        await _db.SaveChangesAsync(ct);
    }
}
