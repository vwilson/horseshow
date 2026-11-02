using HorseShow.Core.Model;
using HorseShow.Core.Scoring;

namespace HorseShow.Core.Tests;

public class PointsCalculatorTests
{
    private static PointSchedule Fixed(params decimal[] points)
    {
        var s = new PointSchedule { Name = "fixed", Mode = PointScheduleMode.Fixed };
        for (var i = 0; i < points.Length; i++)
        {
            s.Rows.Add(new PointScheduleRow { Placing = i + 1, Points = points[i] });
        }
        return s;
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 6)]
    [InlineData(6, 0.5)]
    public void Fixed_schedule_maps_placing_to_points(int placing, decimal expected)
    {
        var s = Fixed(10, 6, 4, 2, 1, 0.5m);
        Assert.Equal(expected, PointsCalculator.PointsFor(s, placing, entryCount: 12));
    }

    [Fact]
    public void Fixed_schedule_ignores_entry_count()
    {
        var s = Fixed(10, 6);
        Assert.Equal(10, PointsCalculator.PointsFor(s, 1, entryCount: 1));
        Assert.Equal(10, PointsCalculator.PointsFor(s, 1, entryCount: 40));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Placing_outside_schedule_returns_null(int placing)
    {
        Assert.Null(PointsCalculator.PointsFor(Fixed(10, 6, 4, 2, 1, 0.5m), placing, 12));
    }

    [Fact]
    public void ByEntryCount_picks_highest_tier_not_exceeding_entries()
    {
        var s = new PointSchedule { Name = "tiered", Mode = PointScheduleMode.ByEntryCount };
        s.Rows.Add(new PointScheduleRow { Placing = 1, Points = 1, MinEntries = 1 });
        s.Rows.Add(new PointScheduleRow { Placing = 1, Points = 3, MinEntries = 3 });
        s.Rows.Add(new PointScheduleRow { Placing = 1, Points = 6, MinEntries = 6 });

        Assert.Equal(1, PointsCalculator.PointsFor(s, 1, entryCount: 2));
        Assert.Equal(3, PointsCalculator.PointsFor(s, 1, entryCount: 5));
        Assert.Equal(6, PointsCalculator.PointsFor(s, 1, entryCount: 6));
        Assert.Equal(6, PointsCalculator.PointsFor(s, 1, entryCount: 30));
    }

    [Fact]
    public void ByEntryCount_returns_null_when_no_tier_is_reached()
    {
        var s = new PointSchedule { Name = "tiered", Mode = PointScheduleMode.ByEntryCount };
        s.Rows.Add(new PointScheduleRow { Placing = 2, Points = 2, MinEntries = 3 });

        Assert.Null(PointsCalculator.PointsFor(s, 2, entryCount: 2));
    }
}
