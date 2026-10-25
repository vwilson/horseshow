using HorseShow.Core.Model;

namespace HorseShow.Data;

/// <summary>Schedules seeded on first run so the app is useful immediately. Users clone and edit these.</summary>
public static class BuiltInSchedules
{
    public static IEnumerable<PointSchedule> All()
    {
        yield return Fixed("Classic 10-6-4-2-1-½", [10m, 6m, 4m, 2m, 1m, 0.5m]);
        yield return Fixed("Linear 6-5-4-3-2-1", [6m, 5m, 4m, 3m, 2m, 1m]);
        yield return Fixed("Top 8: 10-8-6-5-4-3-2-1", [10m, 8m, 6m, 5m, 4m, 3m, 2m, 1m]);
        yield return ByEntryCount();
    }

    private static PointSchedule Fixed(string name, decimal[] points)
    {
        var s = new PointSchedule { Name = name, Mode = PointScheduleMode.Fixed, IsBuiltIn = true };
        for (var i = 0; i < points.Length; i++)
        {
            s.Rows.Add(new PointScheduleRow { Placing = i + 1, Points = points[i], MinEntries = 0 });
        }

        return s;
    }

    /// <summary>
    /// A common "points scale with entries" shape: a placing earns one point per entry it beat,
    /// capped at the top six, with a one-point floor for sixth in a large class.
    /// </summary>
    private static PointSchedule ByEntryCount()
    {
        var s = new PointSchedule { Name = "Scaled by entries (1 to 6)", Mode = PointScheduleMode.ByEntryCount, IsBuiltIn = true };
        for (var entries = 1; entries <= 6; entries++)
        {
            for (var placing = 1; placing <= entries; placing++)
            {
                s.Rows.Add(new PointScheduleRow { Placing = placing, Points = entries - placing + 1, MinEntries = entries });
            }
        }

        return s;
    }
}
