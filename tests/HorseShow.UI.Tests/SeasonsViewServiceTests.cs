using HorseShow.Core.Model;
using HorseShow.UI.Services;

namespace HorseShow.UI.Tests;

public class SeasonsViewServiceTests
{
    [Fact]
    public async Task First_season_becomes_active_and_later_ones_do_not()
    {
        await using var f = await ServiceFixture.CreateAsync();

        var first = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2025", Year = 2025 });
        var second = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026 });

        Assert.True(first.IsActive);
        Assert.False(second.IsActive);
        Assert.Equal(first.Id, (await f.Active.GetAsync())!.Id);
    }

    [Fact]
    public async Task SetActive_is_exclusive()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var a = await f.Seasons.SaveAsync(new SeasonEdit { Name = "A", Year = 2025 });
        var b = await f.Seasons.SaveAsync(new SeasonEdit { Name = "B", Year = 2026 });

        await f.Active.SetActiveAsync(b.Id);

        var rows = await f.Seasons.ListAsync();
        Assert.Single(rows, r => r.Season.IsActive);
        Assert.Equal(b.Id, rows.Single(r => r.Season.IsActive).Season.Id);
        Assert.Equal(b.Id, f.Active.Current!.Id);
        Assert.NotEqual(a.Id, f.Active.Current.Id);
    }

    [Fact]
    public async Task Deleting_active_season_clears_active()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var a = await f.Seasons.SaveAsync(new SeasonEdit { Name = "A", Year = 2025 });

        await f.Seasons.DeleteAsync(a.Id);

        Assert.Null(f.Active.Current);
        Assert.Empty(await f.Seasons.ListAsync());
    }

    [Fact]
    public async Task Division_names_are_unique_per_season()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var s = await f.Seasons.SaveAsync(new SeasonEdit { Name = "A", Year = 2025 });
        await f.Seasons.SaveDivisionAsync(new DivisionEdit { SeasonId = s.Id, Name = "Open" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Seasons.SaveDivisionAsync(new DivisionEdit { SeasonId = s.Id, Name = " Open " }));
    }

    [Fact]
    public async Task Divisions_get_increasing_sort_order()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var s = await f.Seasons.SaveAsync(new SeasonEdit { Name = "A", Year = 2025 });
        var d1 = await f.Seasons.SaveDivisionAsync(new DivisionEdit { SeasonId = s.Id, Name = "One" });
        var d2 = await f.Seasons.SaveDivisionAsync(new DivisionEdit { SeasonId = s.Id, Name = "Two" });

        Assert.True(d2.SortOrder > d1.SortOrder);
    }

    [Fact]
    public async Task Point_schedules_include_built_ins_and_season_specific_only()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var a = await f.Seasons.SaveAsync(new SeasonEdit { Name = "A", Year = 2025 });
        var b = await f.Seasons.SaveAsync(new SeasonEdit { Name = "B", Year = 2026 });
        await using (var db = await f.Db.Factory.CreateDbContextAsync())
        {
            db.PointSchedules.Add(new PointSchedule { Name = "A only", SeasonId = a.Id });
            await db.SaveChangesAsync();
        }

        var forA = await f.Seasons.ListPointSchedulesAsync(a.Id);
        var forB = await f.Seasons.ListPointSchedulesAsync(b.Id);

        Assert.Contains(forA, p => p.Name == "A only");
        Assert.DoesNotContain(forB, p => p.Name == "A only");
        Assert.All(forB, p => Assert.True(p.IsBuiltIn));
    }
}
