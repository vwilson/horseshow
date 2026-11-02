using HorseShow.Core.Model;
using HorseShow.UI.Services;

namespace HorseShow.UI.Tests;

public class ParticipantsViewServiceTests
{
    [Fact]
    public async Task Saving_a_rider_always_includes_the_canonical_alias()
    {
        await using var f = await ServiceFixture.CreateAsync();

        var rider = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Smith, Jane", Aliases = ["J. Smith"] });

        var row = Assert.Single(await f.Participants.ListRidersAsync(includeArchived: false, search: null));
        var aliases = row.Rider.Aliases.Select(a => a.Alias).OrderBy(a => a).ToArray();
        Assert.Equal(["j smith", "jane smith"], aliases);
        Assert.Equal(rider.Id, row.Rider.Id);
    }

    [Fact]
    public async Task Removing_an_alias_from_the_edit_removes_it_from_the_rider()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var rider = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Jane Smith", Aliases = ["janie smith"] });

        await f.Participants.SaveRiderAsync(new RiderEdit { Id = rider.Id, Name = "Jane Smith", Aliases = [] });

        var row = Assert.Single(await f.Participants.ListRidersAsync(false, null));
        Assert.Equal(["jane smith"], row.Rider.Aliases.Select(a => a.Alias).ToArray());
    }

    [Fact]
    public async Task Alias_owned_by_another_rider_is_rejected_with_a_readable_message()
    {
        await using var f = await ServiceFixture.CreateAsync();
        await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Jane Smith" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Participants.SaveRiderAsync(new RiderEdit { Name = "Janet Smith", Aliases = ["Smith, Jane"] }));

        Assert.Contains("Jane Smith", ex.Message);
    }

    [Fact]
    public async Task Search_offers_existing_matches_then_a_create_option()
    {
        await using var f = await ServiceFixture.CreateAsync();
        await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Jane Smith" });
        await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Janet Smythe" });
        await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Bob Jones" });

        var options = (await f.Participants.SearchRiderOptionsAsync("jan")).ToList();

        Assert.Equal(3, options.Count);
        Assert.Equal(["Jane Smith", "Janet Smythe"], options.Take(2).Select(o => o.Name).ToArray());
        Assert.True(options[2].IsNew);
        Assert.Equal("jan", options[2].Name);
    }

    [Fact]
    public async Task Search_does_not_offer_create_when_the_text_matches_exactly()
    {
        await using var f = await ServiceFixture.CreateAsync();
        await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Jane Smith" });

        var options = (await f.Participants.SearchRiderOptionsAsync("Smith, Jane")).ToList();

        var only = Assert.Single(options);
        Assert.False(only.IsNew);
        Assert.Equal("Jane Smith", only.Name);
    }

    [Fact]
    public async Task Search_excludes_archived_and_empty_text_returns_nothing()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var r = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Jane Smith" });
        await f.Participants.SetRiderArchivedAsync(r.Id, true);

        var options = (await f.Participants.SearchRiderOptionsAsync("jane")).ToList();
        Assert.Single(options);
        Assert.True(options[0].IsNew);

        Assert.Empty(await f.Participants.SearchRiderOptionsAsync("   "));
    }

    [Fact]
    public async Task Rider_with_results_cannot_be_deleted_but_can_be_archived()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026 });
        var show = await f.Shows.SaveShowAsync(new ShowEdit { SeasonId = season.Id, Name = "S", StartDate = DateTime.Today });
        var cls = await f.Shows.SaveClassAsync(new ShowClassEdit { ShowId = show.Id, Name = "C" });
        var saved = await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(null, "Jane Smith") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Participants.DeleteRiderAsync(saved.Rider!.Id!.Value));

        await f.Participants.SetRiderArchivedAsync(saved.Rider!.Id!.Value, true);
        Assert.Empty(await f.Participants.ListRidersAsync(includeArchived: false, null));
        Assert.Single(await f.Participants.ListRidersAsync(includeArchived: true, null));
    }

    [Fact]
    public async Task Horse_search_and_save_mirror_rider_behaviour()
    {
        await using var f = await ServiceFixture.CreateAsync();
        await f.Participants.SaveHorseAsync(new HorseEdit { Name = "Blue Moon", Owner = "Jane Smith", Aliases = ["Bluey"] });

        var rows = await f.Participants.ListHorsesAsync(false, "bluey");
        Assert.Single(rows);

        var options = (await f.Participants.SearchHorseOptionsAsync("blue")).ToList();
        Assert.Equal(2, options.Count);
        Assert.Equal("Blue Moon", options[0].Name);
        Assert.True(options[1].IsNew);
    }
}
