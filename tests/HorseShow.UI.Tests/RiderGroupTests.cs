using HorseShow.Core.Model;
using HorseShow.UI.Services;

namespace HorseShow.UI.Tests;

public class RiderGroupTests
{
    [Fact]
    public async Task Groups_are_listed_distinct_and_bulk_assignable()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var a = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "A", Group = "18 & Over" });
        var b = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "B", Group = "18 & over" });
        var c = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "C" });

        Assert.Equal(["18 & Over"], await f.Participants.ListRiderGroupsAsync());

        Assert.Equal(2, await f.Participants.SetRiderGroupAsync([b.Id, c.Id], " 50 & Over "));
        Assert.Equal(["18 & Over", "50 & Over"], await f.Participants.ListRiderGroupsAsync());

        await f.Participants.SetRiderGroupAsync([a.Id], null);
        var rows = await f.Participants.ListRidersAsync(false, null);
        Assert.Null(rows.Single(r => r.Rider.Id == a.Id).Rider.Group);
        Assert.Equal(2, (await f.Participants.ListRidersAsync(false, "50 &")).Count);
    }

    [Fact]
    public async Task Standings_can_filter_by_group_or_section_by_group()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var classic = (await f.Seasons.ListPointSchedulesAsync(null)).First(p => p.Name.StartsWith("Classic"));
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026, DefaultPointScheduleId = classic.Id });
        var show = await f.Shows.SaveShowAsync(new ShowEdit { SeasonId = season.Id, Name = "S", StartDate = DateTime.Today });
        var cls = await f.Shows.SaveClassAsync(new ShowClassEdit { ShowId = show.Id, Name = "Mixed" });
        var master = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Bob Jones", Group = "50 & Over" });
        var adult = await f.Participants.SaveRiderAsync(new RiderEdit { Name = "Jane Smith", Group = "18 & Over" });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 1, Rider = new NameOption(master.Id, master.Name) });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 2, Rider = new NameOption(adult.Id, adult.Name) });
        await f.Shows.SaveResultAsync(new ResultRow { ShowClassId = cls.Id, Placing = 3, Rider = new NameOption(null, "Ann Lee") });

        var all = (await f.Standings.GetAsync(season.Id, null, null))!;
        Assert.Equal(["18 & Over", "50 & Over"], all.RiderGroups);
        Assert.Equal(1, all.UngroupedRiders);
        Assert.Empty(all.Sections);

        var adults = (await f.Standings.GetAsync(season.Id, null, null, riderGroup: "18 & Over"))!;
        var line = Assert.Single(adults.Lines);
        Assert.Equal(("Jane Smith", 1), (line.Name, line.Rank));

        var sectioned = (await f.Standings.GetAsync(season.Id, null, null, sectionByGroup: true))!;
        Assert.Equal(["18 & Over", "50 & Over", "No group"], sectioned.Sections.Select(s => s.Title).ToArray());
        Assert.All(sectioned.Sections, s => Assert.Equal(1, Assert.Single(s.Lines).Rank));

        var byHorse = (await f.Standings.GetAsync(season.Id, StandingsBasis.Horse, null, sectionByGroup: true))!;
        Assert.Empty(byHorse.Sections);
    }
}
