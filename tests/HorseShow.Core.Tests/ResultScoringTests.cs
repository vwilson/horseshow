using HorseShow.Core.Model;
using HorseShow.Core.Scoring;

namespace HorseShow.Core.Tests;

public class ResultScoringTests
{
    private static PointSchedule Fixed(params decimal[] points)
    {
        var s = new PointSchedule { Name = "f", Mode = PointScheduleMode.Fixed };
        for (var i = 0; i < points.Length; i++) s.Rows.Add(new PointScheduleRow { Placing = i + 1, Points = points[i] });
        return s;
    }

    [Fact]
    public void Division_schedule_overrides_season_default()
    {
        var seasonSchedule = Fixed(10);
        var divisionSchedule = Fixed(5);
        var season = new Season { Name = "s", DefaultPointSchedule = seasonSchedule };

        Assert.Same(divisionSchedule, ResultScoring.ScheduleFor(season, new Division { Name = "d", PointSchedule = divisionSchedule }));
        Assert.Same(seasonSchedule, ResultScoring.ScheduleFor(season, new Division { Name = "d" }));
        Assert.Same(seasonSchedule, ResultScoring.ScheduleFor(season, null));
        Assert.Null(ResultScoring.ScheduleFor(new Season { Name = "x" }, null));
    }

    [Fact]
    public void Entry_count_falls_back_to_non_scratched_results()
    {
        var cls = new ShowClass { Name = "c" };
        cls.Results.Add(new Result { Placing = 1 });
        cls.Results.Add(new Result { Placing = 2 });
        cls.Results.Add(new Result { PlacingStatus = PlacingStatus.Scratched });

        Assert.Equal(2, ResultScoring.EffectiveEntryCount(cls));
        Assert.True(ResultScoring.UsesFallbackEntryCount(cls));

        cls.EntryCount = 8;
        Assert.Equal(8, ResultScoring.EffectiveEntryCount(cls));
        Assert.False(ResultScoring.UsesFallbackEntryCount(cls));
    }

    [Fact]
    public void Placed_beyond_schedule_scores_zero_but_unplaced_is_null()
    {
        var schedule = Fixed(10, 6);

        Assert.Equal(10, ResultScoring.ComputePoints(schedule, new Result { Placing = 1 }, 5));
        Assert.Equal(0, ResultScoring.ComputePoints(schedule, new Result { Placing = 3 }, 5));
        Assert.Null(ResultScoring.ComputePoints(schedule, new Result { Placing = 1, PlacingStatus = PlacingStatus.Disqualified }, 5));
        Assert.Null(ResultScoring.ComputePoints(schedule, new Result { Placing = null }, 5));
        Assert.Null(ResultScoring.ComputePoints(null, new Result { Placing = 1 }, 5));
    }
}
