using HorseShow.Core.Model;

namespace HorseShow.Core.Scoring;

/// <summary>Decides which schedule applies to a class and what a result under it is worth.</summary>
public static class ResultScoring
{
    /// <summary>Division override first, then the season default. Null when neither is set.</summary>
    public static PointSchedule? ScheduleFor(Season season, Division? division)
        => division?.PointSchedule ?? season.DefaultPointSchedule;

    /// <summary>
    /// The class's recorded entry count, or, when it was not recorded, the number of results
    /// that actually competed (everything except scratches). Published results often list only
    /// the top placings, so the fallback undercounts and callers should flag it.
    /// </summary>
    public static int EffectiveEntryCount(ShowClass cls)
        => cls.EntryCount ?? cls.Results.Count(r => r.PlacingStatus != PlacingStatus.Scratched);

    public static bool UsesFallbackEntryCount(ShowClass cls) => cls.EntryCount is null;

    /// <summary>
    /// Points for a result. Null means "not scored": no schedule, or the entry did not place.
    /// A placed entry beyond the schedule's last row scores zero, not null, so it still counts as
    /// an appearance in standings.
    /// </summary>
    public static decimal? ComputePoints(PointSchedule? schedule, Result result, int entryCount)
    {
        if (schedule is null || result.PlacingStatus != PlacingStatus.Placed || result.Placing is not int placing)
        {
            return null;
        }

        return PointsCalculator.PointsFor(schedule, placing, entryCount) ?? 0m;
    }
}
