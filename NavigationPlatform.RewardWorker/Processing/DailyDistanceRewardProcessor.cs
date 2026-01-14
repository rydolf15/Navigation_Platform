using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.RewardWorker.Persistence;
using NavigationPlatform.RewardWorker.Persistence.Outbox;

namespace NavigationPlatform.RewardWorker.Processing;

internal sealed record DailyGoalEvaluationResult(
    Guid UserId,
    DateOnly Date,
    decimal TotalDistanceKm,
    bool IsGoalAchieved,
    bool WasAwardedNow,
    Guid? GrantedByJourneyId);

internal sealed class DailyDistanceRewardProcessor
{
    private readonly RewardDbContext _db;

    public DailyDistanceRewardProcessor(RewardDbContext db)
    {
        _db = db;
    }

    public async Task<DailyGoalEvaluationResult> UpsertAsync(
        IJourneyDistanceEvent evt,
        CancellationToken ct = default)
    {
        var journeyId = evt.JourneyId;
        var userId = evt.UserId;
        var date = DateOnly.FromDateTime(evt.StartTime);
        var distanceKm = evt.DistanceKm;

        var existing = await _db.Journeys.FindAsync([journeyId], ct);

        DailyDistanceProjection? updatedDaily = null;
        var awardedNow = false;

        if (existing == null)
        {
            existing = new JourneyProjection
            {
                JourneyId = journeyId,
                UserId = userId,
                Date = date,
                DistanceKm = distanceKm
            };
            _db.Journeys.Add(existing);

            (updatedDaily, awardedNow) = await ApplyDeltaAsync(userId, date, +distanceKm, triggeringJourneyId: journeyId, ct);
        }
        else
        {
            var oldUserId = existing.UserId;
            var oldDate = existing.Date;
            var oldDistance = existing.DistanceKm;

            existing.UserId = userId;
            existing.Date = date;
            existing.DistanceKm = distanceKm;

            if (oldUserId == userId && oldDate == date)
            {
                (updatedDaily, awardedNow) = await ApplyDeltaAsync(
                    userId,
                    date,
                    distanceKm - oldDistance,
                    triggeringJourneyId: journeyId,
                    ct);
            }
            else
            {
                // Remove from previous bucket and add to the new one.
                await ApplyDeltaAsync(oldUserId, oldDate, -oldDistance, triggeringJourneyId: journeyId, ct);
                (updatedDaily, awardedNow) = await ApplyDeltaAsync(userId, date, +distanceKm, triggeringJourneyId: journeyId, ct);
            }
        }

        updatedDaily ??= await _db.DailyDistances.FindAsync([userId, date], ct);

        return updatedDaily == null
            ? new DailyGoalEvaluationResult(userId, date, 0, IsGoalAchieved: false, WasAwardedNow: false, GrantedByJourneyId: null)
            : new DailyGoalEvaluationResult(
                updatedDaily.UserId,
                updatedDaily.Date,
                updatedDaily.TotalDistanceKm,
                IsGoalAchieved: updatedDaily.RewardGranted,
                WasAwardedNow: awardedNow,
                GrantedByJourneyId: updatedDaily.GrantedByJourneyId);
    }

    public async Task<DailyGoalEvaluationResult> DeleteAsync(
        JourneyDeleted evt,
        CancellationToken ct = default)
    {
        var journeyId = evt.JourneyId;

        var existing = await _db.Journeys.FindAsync([journeyId], ct);
        if (existing == null)
        {
            // Nothing to delete; still return the current state for the day from event metadata.
            var userId = evt.UserId;
            var date = DateOnly.FromDateTime(evt.StartTime);
            var daily = await _db.DailyDistances.FindAsync([userId, date], ct);

            return daily == null
                ? new DailyGoalEvaluationResult(userId, date, 0, IsGoalAchieved: false, WasAwardedNow: false, GrantedByJourneyId: null)
                : new DailyGoalEvaluationResult(
                    daily.UserId,
                    daily.Date,
                    daily.TotalDistanceKm,
                    IsGoalAchieved: daily.RewardGranted,
                    WasAwardedNow: false,
                    GrantedByJourneyId: daily.GrantedByJourneyId);
        }

        _db.Journeys.Remove(existing);

        var (updatedDaily, _) = await ApplyDeltaAsync(
            existing.UserId,
            existing.Date,
            -existing.DistanceKm,
            triggeringJourneyId: journeyId,
            ct);

        updatedDaily ??= await _db.DailyDistances.FindAsync([existing.UserId, existing.Date], ct);

        return updatedDaily == null
            ? new DailyGoalEvaluationResult(existing.UserId, existing.Date, 0, IsGoalAchieved: false, WasAwardedNow: false, GrantedByJourneyId: null)
            : new DailyGoalEvaluationResult(
                updatedDaily.UserId,
                updatedDaily.Date,
                updatedDaily.TotalDistanceKm,
                IsGoalAchieved: updatedDaily.RewardGranted,
                WasAwardedNow: false,
                GrantedByJourneyId: updatedDaily.GrantedByJourneyId);
    }

    private async Task<(DailyDistanceProjection? Daily, bool AwardedNow)> ApplyDeltaAsync(
        Guid userId,
        DateOnly date,
        decimal deltaKm,
        Guid triggeringJourneyId,
        CancellationToken ct)
    {
        var daily = await _db.DailyDistances.FindAsync([userId, date], ct);
        if (daily == null)
        {
            if (deltaKm == 0)
                return (null, false);

            daily = new DailyDistanceProjection
            {
                UserId = userId,
                Date = date,
                TotalDistanceKm = 0,
                RewardGranted = false
            };
            _db.DailyDistances.Add(daily);
        }

        if (deltaKm == 0)
            return (daily, false);

        var before = daily.TotalDistanceKm;

        daily.TotalDistanceKm += deltaKm;
        if (daily.TotalDistanceKm < 0)
            daily.TotalDistanceKm = 0;

        var after = daily.TotalDistanceKm;

        // Grant only once/day and only on the transition from below->above threshold.
        var awardedNow =
            !daily.RewardGranted &&
            !DailyRewardEvaluator.ShouldGrant(before) &&
            DailyRewardEvaluator.ShouldGrant(after);

        if (awardedNow)
        {
            daily.RewardGranted = true;
            daily.GrantedByJourneyId = triggeringJourneyId;

            _db.OutboxMessages.Add(
                OutboxMessage.From(
                    new JourneyDailyGoalAchieved(
                        triggeringJourneyId,
                        userId,
                        date,
                        after)));
        }

        return (daily, awardedNow);
    }
}

