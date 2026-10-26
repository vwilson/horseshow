using HorseShow.Core.Model;
using HorseShow.UI.Services;

namespace HorseShow.UI.Tests;

public class SchedulesViewServiceTests
{
    [Fact]
    public async Task List_shows_built_ins_with_usage_counts()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var classic = (await f.Seasons.ListPointSchedulesAsync(null)).First(p => p.Name.StartsWith("Classic"));
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026, DefaultPointScheduleId = classic.Id });
        await f.Seasons.SaveDivisionAsync(new DivisionEdit { SeasonId = season.Id, Name = "Open", PointScheduleId = classic.Id });

        var rows = await f.Schedules.ListAsync(season.Id);

        var row = rows.Single(r => r.Schedule.Id == classic.Id);
        Assert.Equal(1, row.UsedBySeasons);
        Assert.Equal(1, row.UsedByDivisions);
        Assert.Equal(6, row.RowCount);
        Assert.All(rows, r => Assert.True(r.Schedule.IsBuiltIn));
    }

    [Fact]
    public async Task Built_in_is_read_only_but_clone_is_editable()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var classic = (await f.Seasons.ListPointSchedulesAsync(null)).First(p => p.Name.StartsWith("Classic"));

        var edit = PointScheduleEdit.From((await f.Schedules.GetAsync(classic.Id))!);
        edit.Name = "hacked";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Schedules.SaveAsync(edit));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Schedules.DeleteAsync(classic.Id));

        var copy = await f.Schedules.CloneAsync(classic.Id, seasonId: null);
        Assert.False(copy.IsBuiltIn);
        Assert.EndsWith("(copy)", copy.Name);
        Assert.Equal(6, copy.Rows.Count);

        var copyEdit = PointScheduleEdit.From((await f.Schedules.GetAsync(copy.Id))!);
        copyEdit.Name = "Mine";
        copyEdit.Rows.RemoveAt(copyEdit.Rows.Count - 1);
        var saved = await f.Schedules.SaveAsync(copyEdit);
        Assert.Equal("Mine", saved.Name);
        Assert.Equal(5, (await f.Schedules.GetAsync(saved.Id))!.Rows.Count);
    }

    [Theory]
    [InlineData("", 1, 10, 0, "name")]
    [InlineData("x", 0, 10, 0, "Placings start")]
    [InlineData("x", 1, -1, 0, "negative")]
    public async Task Save_rejects_bad_input(string name, int placing, decimal points, int minEntries, string messagePart)
    {
        await using var f = await ServiceFixture.CreateAsync();
        var edit = new PointScheduleEdit { Name = name, Rows = [new PointScheduleRowEdit { Placing = placing, Points = points, MinEntries = minEntries }] };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Schedules.SaveAsync(edit));
        Assert.Contains(messagePart, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_placing_in_same_tier_is_rejected_but_different_tiers_are_fine()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var dup = new PointScheduleEdit
        {
            Name = "d", Mode = PointScheduleMode.Fixed,
            Rows = [new() { Placing = 1, Points = 5 }, new() { Placing = 1, Points = 6 }],
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Schedules.SaveAsync(dup));

        var tiered = new PointScheduleEdit
        {
            Name = "t", Mode = PointScheduleMode.ByEntryCount,
            Rows = [new() { Placing = 1, Points = 1, MinEntries = 1 }, new() { Placing = 1, Points = 3, MinEntries = 3 }],
        };
        var saved = await f.Schedules.SaveAsync(tiered);
        Assert.Equal(2, (await f.Schedules.GetAsync(saved.Id))!.Rows.Count);
    }

    [Fact]
    public async Task Editing_a_schedule_in_use_recalculates_the_seasons_using_it()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var classic = (await f.Seasons.ListPointSchedulesAsync(null)).First(p => p.Name.StartsWith("Classic"));
        var mine = await f.Schedules.CloneAsync(classic.Id, null);
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026, DefaultPointScheduleId = mine.Id });
        var show = await f.Shows.SaveShowAsync(new ShowEdit { SeasonId = season.Id, Name = "S", StartDate = DateTime.Today });
        var cls = await f.Shows.SaveClassAsync(new ShowClassEdit { ShowId = show.Id, Name = "C" });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "A") });

        var edit = PointScheduleEdit.From((await f.Schedules.GetAsync(mine.Id))!);
        edit.Rows.Single(r => r.Placing == 1).Points = 100;
        await f.Schedules.SaveAsync(edit);

        Assert.Equal(100, Assert.Single(await f.Shows.GetClassResultsAsync(cls.Id)).Points);

        await f.Schedules.DeleteAsync(mine.Id);
        Assert.Null(Assert.Single(await f.Shows.GetClassResultsAsync(cls.Id)).Points);
        Assert.Null((await f.Seasons.GetAsync(season.Id))!.DefaultPointScheduleId);
    }
}
