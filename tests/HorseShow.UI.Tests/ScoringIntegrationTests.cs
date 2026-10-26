using HorseShow.Core.Model;
using HorseShow.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Tests;

/// <summary>Points flow through the view services: save a result, points appear; change a schedule, points move.</summary>
public class ScoringIntegrationTests
{
    private static async Task<int> ClassicScheduleIdAsync(ServiceFixture f)
    {
        var schedules = await f.Seasons.ListPointSchedulesAsync(null);
        return schedules.First(p => p.Name.StartsWith("Classic")).Id;
    }

    private static async Task<(Season season, Show show, ShowClass cls)> SeedAsync(ServiceFixture f, int? scheduleId, int? entryCount = null)
    {
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026, DefaultPointScheduleId = scheduleId });
        var show = await f.Shows.SaveShowAsync(new ShowEdit { SeasonId = season.Id, Name = "S", StartDate = new DateTime(2026, 4, 1) });
        var cls = await f.Shows.SaveClassAsync(new ShowClassEdit { ShowId = show.Id, Name = "C", EntryCount = entryCount });
        return (season, show, cls);
    }

    [Fact]
    public async Task Saving_a_result_computes_its_points_from_the_season_schedule()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (_, _, cls) = await SeedAsync(f, await ClassicScheduleIdAsync(f));

        var first = await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "A") });
        var second = await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 2, Rider = new NameOption(null, "B") });

        Assert.Equal(10, first.Points);
        Assert.Equal(6, second.Points);
        Assert.Equal(PointsSource.Computed, first.PointsSource);
    }

    [Fact]
    public async Task Manual_points_survive_recalculation_and_clearing_them_restores_computed()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (season, _, cls) = await SeedAsync(f, await ClassicScheduleIdAsync(f));
        var row = await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "A") });

        row.Points = 42;
        row.PointsSource = PointsSource.Manual;
        var manual = await f.Shows.SaveResultAsync(row);
        Assert.Equal(42, manual.Points);
        Assert.Equal(PointsSource.Manual, manual.PointsSource);
        Assert.Empty(await f.Standings.PreviewRecalculationAsync(season.Id));

        manual.Points = null;
        manual.PointsSource = PointsSource.Computed;
        var restored = await f.Shows.SaveResultAsync(manual);
        Assert.Equal(10, restored.Points);
        Assert.Equal(PointsSource.Computed, restored.PointsSource);
    }

    [Fact]
    public async Task Adding_a_result_under_an_entry_scaled_schedule_moves_existing_points()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var scaled = (await f.Seasons.ListPointSchedulesAsync(null)).First(p => p.Mode == PointScheduleMode.ByEntryCount);
        var (_, _, cls) = await SeedAsync(f, scaled.Id); // no recorded entry count: row count is the entry count

        var a = await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "A") });
        Assert.Equal(1, a.Points); // 1 entry: first earns 1

        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 2, Rider = new NameOption(null, "B") });
        var rows = await f.Shows.GetClassResultsAsync(cls.Id);
        Assert.Equal(2, rows.Single(r => r.Rider!.Name == "A").Points); // 2 entries: first earns 2
        Assert.Equal(1, rows.Single(r => r.Rider!.Name == "B").Points);
    }

    [Fact]
    public async Task Changing_the_season_schedule_recalculates_and_reports_the_count()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (season, _, cls) = await SeedAsync(f, await ClassicScheduleIdAsync(f));
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "A") });
        var linear = (await f.Seasons.ListPointSchedulesAsync(null)).First(p => p.Name.StartsWith("Linear"));

        var edit = SeasonEdit.From((await f.Seasons.GetAsync(season.Id))!);
        edit.DefaultPointScheduleId = linear.Id;
        await f.Seasons.SaveAsync(edit);

        Assert.Equal(1, f.Seasons.LastRecalculatedCount);
        var row = Assert.Single(await f.Shows.GetClassResultsAsync(cls.Id));
        Assert.Equal(6, row.Points);
    }

    [Fact]
    public async Task Standings_page_reflects_points_division_filter_and_warnings()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (season, show, cls) = await SeedAsync(f, await ClassicScheduleIdAsync(f));
        var open = await f.Seasons.SaveDivisionAsync(new DivisionEdit { SeasonId = season.Id, Name = "Open" });
        var openClass = await f.Shows.SaveClassAsync(new ShowClassEdit { ShowId = show.Id, Name = "Open class", DivisionId = open.Id, EntryCount = 8 });

        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "A"), Horse = new NameOption(null, "H1") });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 2, Rider = new NameOption(null, "B"), Horse = new NameOption(null, "H2") });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = openClass.Id, Placing = 1, Rider = new NameOption(null, "B"), Horse = new NameOption(null, "H2") });

        var overall = (await f.Standings.GetAsync(season.Id, null, null))!;
        Assert.Equal(["B", "A"], overall.Lines.Select(l => l.Name).ToArray());
        Assert.Equal(16, overall.Lines[0].CountedPoints);
        Assert.Equal(1, overall.UnassignedClasses);
        Assert.Equal(1, overall.FallbackEntryCountClasses);
        Assert.True(overall.HasSchedule);

        var openOnly = (await f.Standings.GetAsync(season.Id, StandingsBasis.Horse, open.Id))!;
        var line = Assert.Single(openOnly.Lines);
        Assert.Equal("H2", line.Name);
        Assert.Equal(10, line.CountedPoints);

        var summary = await f.Dashboard.GetSummaryAsync(); // first season created is the active one
        Assert.Equal(["B", "A"], summary.Leaders.Select(l => l.Name).ToArray());
    }

    [Fact]
    public async Task Recalculate_preview_and_apply_round_trip()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (season, _, cls) = await SeedAsync(f, await ClassicScheduleIdAsync(f));
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "A") });

        // Simulate stale points written by something that bypassed the engine.
        await using (var db = await f.Db.Factory.CreateDbContextAsync())
        {
            await db.Results.ExecuteUpdateAsync(u => u.SetProperty(r => r.Points, 1m));
        }

        var preview = await f.Standings.PreviewRecalculationAsync(season.Id);
        var change = Assert.Single(preview);
        Assert.Equal(1, change.OldPoints);
        Assert.Equal(10, change.NewPoints);
        Assert.Equal("A", change.Competitor);

        Assert.Equal(1, await f.Standings.RecalculateAsync(season.Id));
        Assert.Empty(await f.Standings.PreviewRecalculationAsync(season.Id));
    }
}
