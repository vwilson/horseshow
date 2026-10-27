using HorseShow.Core.Model;
using HorseShow.Core.Scoring;

namespace HorseShow.Core.Tests;

public class RiderGroupStandingsTests
{
    private static readonly Rider Adult = new() { Id = 1, Name = "Jane Smith", Group = "18 & Over" };
    private static readonly Rider Master = new() { Id = 2, Name = "Bob Jones", Group = "50 & over" };
    private static readonly Rider NoGroup = new() { Id = 3, Name = "Ann Lee" };
    private static readonly Horse Blue = new() { Id = 10, Name = "Blue Moon" };

    private static Result R(Rider rider, int placing, decimal points)
    {
        var show = new Show { Id = 1, Name = "S", StartDate = new DateOnly(2026, 4, 1) };
        return new Result { ShowClass = new ShowClass { Id = 1, Name = "Mixed class", Show = show, ShowId = 1 }, Rider = rider, RiderId = rider.Id, Horse = Blue, HorseId = 10, Placing = placing, Points = points };
    }

    [Fact]
    public void Group_filter_ranks_only_riders_in_that_group()
    {
        var results = new[] { R(Master, 1, 10), R(Adult, 2, 6), R(NoGroup, 3, 4) };

        var adults = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider, RiderGroup: "18 & over"));
        var masters = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider, RiderGroup: "50 & Over"));
        var ungrouped = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider, RiderGroup: ""));
        var all = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider));

        var adult = Assert.Single(adults);
        Assert.Equal("Jane Smith", adult.Name);
        Assert.Equal(1, adult.Rank); // first among adults even though second in the class
        Assert.Equal("18 & Over", adult.RiderGroup);
        Assert.Equal("Bob Jones", Assert.Single(masters).Name);
        Assert.Equal("Ann Lee", Assert.Single(ungrouped).Name);
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void Horse_basis_ignores_rider_group_on_lines()
    {
        var lines = new StandingsCalculator().Compute([R(Adult, 1, 10)], new StandingsOptions(StandingsBasis.Horse));
        Assert.Null(Assert.Single(lines).RiderGroup);
    }
}
