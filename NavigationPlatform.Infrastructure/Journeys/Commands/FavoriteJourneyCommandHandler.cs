using MediatR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Favourites;

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

        await _db.SaveChangesAsync(ct);
    }
}
