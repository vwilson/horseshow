using HorseShow.Core.Model;

namespace HorseShow.Core.Scoring;

/// <summary>Maps a placing to points under a schedule. Pure; fully covered by Core tests.</summary>
public static class PointsCalculator
{
    /// <summary>
    /// Returns the points for <paramref name="placing"/> under <paramref name="schedule"/>, or null
    /// when the schedule has no row for that placing (which is "zero points, not an error").
    /// </summary>
    public static decimal? PointsFor(PointSchedule schedule, int placing, int entryCount)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (placing < 1)
        {
            return null;
        }

        var candidates = schedule.Rows.Where(r => r.Placing == placing);

        if (schedule.Mode == PointScheduleMode.ByEntryCount)
        {
            candidates = candidates.Where(r => r.MinEntries <= entryCount);
        }

        var row = candidates.OrderByDescending(r => r.MinEntries).FirstOrDefault();
        return row?.Points;
    }
}
