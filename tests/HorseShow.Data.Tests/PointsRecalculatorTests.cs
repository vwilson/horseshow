using HorseShow.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Data.Tests;

public class PointsRecalculatorTests
{
    private static async Task<(int seasonId, int classId)> SeedAsync(TempDatabase tmp, int? entryCount = null, bool withSchedule = true)
    {
        await tmp.Initializer.InitializeAsync();
        await using var db = await tmp.Factory.CreateDbContextAsync();
        var schedule = withSchedule ? await db.PointSchedules.FirstAsync(p => p.Name.StartsWith("Classic")) : null;
        var season = new Season { Name = "2026", Year = 2026, IsActive = true, DefaultPointSchedule = schedule };
        var show = new Show { Name = "S", StartDate = new DateOnly(2026, 4, 1), Season = season };
        var cls = new ShowClass { Name = "C", Show = show, EntryCount = entryCount };
        cls.Results.Add(new Result { Placing = 1, Rider = new Rider { Name = "A" } });
        cls.Results.Add(new Result { Placing = 2, Rider = new Rider { Name = "B" } });
        cls.Results.Add(new Result { Placing = 9, Rider = new Rider { Name = "C" } });
        cls.Results.Add(new Result { Placing = 1, Rider = new Rider { Name = "M" }, Points = 99, PointsSource = PointsSource.Manual });
        cls.Results.Add(new Result { PlacingStatus = PlacingStatus.Scratched, Rider = new Rider { Name = "S" } });
        db.Add(cls);
        await db.SaveChangesAsync();
        return (season.Id, cls.Id);
    }

    [Fact]
    public async Task Recalculating_a_class_fills_computed_points_and_leaves_manual_alone()
    {
        await using var tmp = new TempDatabase();
        var (_, classId) = await SeedAsync(tmp);

        await using (var db = await tmp.Factory.CreateDbContextAsync())
        {
            var changed = await PointsRecalculator.RecalculateClassAsync(db, classId);
            await db.SaveChangesAsync();
            Assert.Equal(3, changed); // A, B, C; M is manual and S is scratched (null stays null)
        }

        await using var check = await tmp.Factory.CreateDbContextAsync();
        var byName = await check.Results.Include(r => r.Rider).ToDictionaryAsync(r => r.Rider!.Name, r => r.Points);
        Assert.Equal(10, byName["A"]);
        Assert.Equal(6, byName["B"]);
        Assert.Equal(0, byName["C"]);
        Assert.Equal(99, byName["M"]);
        Assert.Null(byName["S"]);
    }

    [Fact]
    public async Task Preview_lists_changes_without_saving_and_apply_persists_them()
    {
        await using var tmp = new TempDatabase();
        var (seasonId, _) = await SeedAsync(tmp);

        await using var db = await tmp.Factory.CreateDbContextAsync();
        var preview = await PointsRecalculator.PreviewSeasonAsync(db, seasonId);
        Assert.Equal(3, preview.Count);
        Assert.Contains(preview, c => c.Competitor == "A" && c.OldPoints is null && c.NewPoints == 10);
        Assert.All(await db.Results.ToListAsync(), r => Assert.True(r.Points is null || r.PointsSource == PointsSource.Manual));

        var changed = await PointsRecalculator.RecalculateSeasonAsync(db, seasonId);
        Assert.Equal(3, changed);
        Assert.Empty(await PointsRecalculator.PreviewSeasonAsync(db, seasonId));
    }

    [Fact]
    public async Task Without_a_schedule_points_are_cleared()
    {
        await using var tmp = new TempDatabase();
        var (seasonId, classId) = await SeedAsync(tmp);

        await using var db = await tmp.Factory.CreateDbContextAsync();
        await PointsRecalculator.RecalculateSeasonAsync(db, seasonId);
        var season = await db.Seasons.FirstAsync();
        season.DefaultPointScheduleId = null;
        await db.SaveChangesAsync();

        var changed = await PointsRecalculator.RecalculateSeasonAsync(db, seasonId);

        Assert.Equal(3, changed);
        Assert.All(await db.Results.Where(r => r.PointsSource == PointsSource.Computed).ToListAsync(), r => Assert.Null(r.Points));
    }
}
