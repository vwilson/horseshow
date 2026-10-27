using HorseShow.Core.Model;
using HorseShow.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Tests;

public class MergeTests
{
    [Fact]
    public async Task Merging_riders_moves_results_and_aliases_and_deletes_sources()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026 });
        var show = await f.Shows.SaveShowAsync(new ShowEdit { SeasonId = season.Id, Name = "S", StartDate = DateTime.Today });
        var cls = await f.Shows.SaveClassAsync(new ShowClassEdit { ShowId = show.Id, Name = "C" });
        var keep = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Jane Smith" });
        var dup = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "J. Smith", Aliases = ["janie"] });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(dup.Id, dup.Name) });

        var moved = await f.Participants.MergeRidersAsync(keep.Id, [dup.Id, keep.Id]);

        Assert.Equal(1, moved);
        var rows = await f.Participants.ListRidersAsync(true, null);
        var only = Assert.Single(rows);
        Assert.Equal(keep.Id, only.Rider.Id);
        Assert.Equal(1, only.ResultCount);
        Assert.Equal(["j smith", "jane smith", "janie"], only.Rider.Aliases.Select(a => a.Alias).OrderBy(a => a).ToArray());
    }

    [Fact]
    public async Task Merging_horses_mirrors_riders()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var keep = await f.Participants.SaveHorseAsync(new HorseEdit { Name = "Blue Moon" });
        var dup = await f.Participants.SaveHorseAsync(new HorseEdit { Name = "Blue Moon (reg)" });

        Assert.Equal(0, await f.Participants.MergeHorsesAsync(keep.Id, [dup.Id]));

        await using var db = await f.Db.Factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Horses.CountAsync());
        Assert.Equal(2, await db.HorseAliases.CountAsync(a => a.HorseId == keep.Id));
    }
}
