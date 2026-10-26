using HorseShow.Core.Model;
using HorseShow.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Tests;

public class ShowsViewServiceTests
{
    private static async Task<(Season season, Show show, ShowClass cls)> SeedAsync(ServiceFixture f)
    {
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026 });
        var show = await f.Shows.SaveShowAsync(new ShowEdit { SeasonId = season.Id, Name = "Spring Classic", StartDate = new DateTime(2026, 4, 12) });
        var cls = await f.Shows.SaveClassAsync(new ShowClassEdit { ShowId = show.Id, Name = "Hunter Under Saddle", ClassNumber = "12" });
        return (season, show, cls);
    }

    [Fact]
    public async Task Saving_a_result_with_new_names_creates_rider_and_horse_with_aliases()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (_, _, cls) = await SeedAsync(f);

        var saved = await f.Shows.SaveResultAsync(new ResultRow
        {
            ShowClassId = cls.Id,
            Placing = 1,
            BackNumber = "204",
            Rider = new NameOption(null, "Smith, Jane"),
            Horse = new NameOption(null, "Blue Moon"),
        });

        Assert.NotEqual(0, saved.Id);
        Assert.NotNull(saved.Rider!.Id);
        Assert.NotNull(saved.Horse!.Id);

        await using var db = await f.Db.Factory.CreateDbContextAsync();
        var rider = await db.Riders.Include(r => r.Aliases).SingleAsync();
        Assert.Equal("Smith, Jane", rider.Name);
        Assert.Contains(rider.Aliases, a => a.Alias == "jane smith");
    }

    [Fact]
    public async Task Same_name_spelled_differently_resolves_to_the_same_rider()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (_, _, cls) = await SeedAsync(f);

        var r1 = await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "Smith, Jane") });
        var r2 = await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 2, Rider = new NameOption(null, "  JANE SMITH ") });

        Assert.Equal(r1.Rider!.Id, r2.Rider!.Id);
        await using var db = await f.Db.Factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Riders.CountAsync());
    }

    [Fact]
    public async Task Result_without_rider_or_horse_is_rejected_before_hitting_the_database()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (_, _, cls) = await SeedAsync(f);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1 }));
    }

    [Fact]
    public async Task Placed_result_needs_a_placing_but_scratched_does_not()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (_, _, cls) = await SeedAsync(f);
        var rider = new NameOption(null, "Jane Smith");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Rider = rider }));

        var scratched = await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Rider = rider, Status = PlacingStatus.Scratched, Placing = 3 });
        Assert.Null(scratched.Placing);
        Assert.Equal(PlacingStatus.Scratched, scratched.Status);
    }

    [Fact]
    public async Task Editing_a_result_can_swap_the_horse_and_clear_the_rider()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (_, _, cls) = await SeedAsync(f);
        var saved = await f.Shows.SaveResultAsync(new ResultRow
        {
            ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "Jane Smith"), Horse = new NameOption(null, "Blue Moon"),
        });

        saved.Rider = null;
        saved.Horse = new NameOption(null, "Red Sky");
        saved.Placing = 4;
        var updated = await f.Shows.SaveResultAsync(saved);

        Assert.Equal(saved.Id, updated.Id);
        Assert.Null(updated.Rider);
        Assert.Equal("Red Sky", updated.Horse!.Name);
        Assert.Equal(4, updated.Placing);

        await using var db = await f.Db.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.Horses.CountAsync());
    }

    [Fact]
    public async Task Results_are_ordered_by_placing_with_unplaced_last()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (_, _, cls) = await SeedAsync(f);
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Rider = new NameOption(null, "C"), Status = PlacingStatus.Scratched });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Rider = new NameOption(null, "B"), Placing = 2 });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Rider = new NameOption(null, "A"), Placing = 1 });

        var rows = await f.Shows.GetClassResultsAsync(cls.Id);

        Assert.Equal(["A", "B", "C"], rows.Select(r => r.Rider!.Name).ToArray());
    }

    [Fact]
    public async Task Deleting_a_show_cascades_to_results_but_keeps_participants()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (season, show, cls) = await SeedAsync(f);
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "Jane Smith") });

        await f.Shows.DeleteShowAsync(show.Id);

        await using var db = await f.Db.Factory.CreateDbContextAsync();
        Assert.Equal(0, await db.Results.CountAsync());
        Assert.Equal(0, await db.ShowClasses.CountAsync());
        Assert.Equal(1, await db.Riders.CountAsync());
        Assert.Empty(await f.Shows.ListAsync(season.Id));
    }

    [Fact]
    public async Task List_counts_classes_and_results()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var (season, show, cls) = await SeedAsync(f);
        await f.Shows.SaveClassAsync(new ShowClassEdit { ShowId = show.Id, Name = "Equitation" });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "Jane Smith") });

        var row = Assert.Single(await f.Shows.ListAsync(season.Id));
        Assert.Equal(2, row.ClassCount);
        Assert.Equal(1, row.ResultCount);
    }
}
