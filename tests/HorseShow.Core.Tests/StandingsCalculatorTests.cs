using HorseShow.Core.Model;
using HorseShow.Core.Scoring;

namespace HorseShow.Core.Tests;

public class StandingsCalculatorTests
{
    private static readonly Rider Jane = new() { Id = 1, Name = "Jane Smith" };
    private static readonly Rider Bob = new() { Id = 2, Name = "Bob Jones" };
    private static readonly Rider Ann = new() { Id = 3, Name = "Ann Lee" };
    private static readonly Horse Blue = new() { Id = 10, Name = "Blue Moon" };
    private static readonly Horse Red = new() { Id = 11, Name = "Red Sky" };
    private static readonly Division Open = new() { Id = 100, SeasonId = 1, Name = "Open" };
    private static readonly Division Green = new() { Id = 101, SeasonId = 1, Name = "Green" };

    private static Show ShowOn(int id, int month) => new() { Id = id, Name = $"Show {id}", StartDate = new DateOnly(2026, month, 1), SeasonId = 1 };

    private static Result R(Show show, Rider? rider, Horse? horse, int? placing, decimal? points, Division? division = null, PlacingStatus status = PlacingStatus.Placed)
    {
        var cls = new ShowClass { Id = show.Id * 10 + (division?.Id ?? 0), Name = "C", Show = show, ShowId = show.Id, Division = division, DivisionId = division?.Id };
        return new Result { ShowClass = cls, Rider = rider, RiderId = rider?.Id, Horse = horse, HorseId = horse?.Id, Placing = placing, PlacingStatus = status, Points = points };
    }

    [Fact]
    public void Sums_points_per_rider_and_ranks_by_total()
    {
        var s1 = ShowOn(1, 4);
        var s2 = ShowOn(2, 5);
        var results = new[]
        {
            R(s1, Jane, Blue, 1, 10), R(s1, Bob, Red, 2, 6),
            R(s2, Jane, Blue, 3, 4), R(s2, Bob, Red, 1, 10),
        };

        var lines = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider));

        Assert.Equal(["Bob Jones", "Jane Smith"], lines.Select(l => l.Name).ToArray());
        Assert.Equal(16, lines[0].TotalPoints);
        Assert.Equal(14, lines[1].TotalPoints);
        Assert.Equal(1, lines[0].Rank);
        Assert.Equal(2, lines[1].Rank);
        Assert.Equal(2, lines[0].Shows);
        Assert.Equal(2, lines[0].PerShow.Count);
    }

    [Fact]
    public void Ties_share_a_rank_and_break_on_firsts()
    {
        var s1 = ShowOn(1, 4);
        var results = new[]
        {
            R(s1, Jane, Blue, 1, 10),
            R(s1, Bob, Red, 2, 10),
            R(s1, Ann, Red, 3, 10),
        };

        var lines = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider));

        // Equal points; the first, then the second, then the third place break the tie.
        Assert.Equal(["Jane Smith", "Bob Jones", "Ann Lee"], lines.Select(l => l.Name).ToArray());
        Assert.Equal([1, 2, 3], lines.Select(l => l.Rank).ToArray());
    }

    [Fact]
    public void Exact_ties_share_rank_and_next_rank_skips()
    {
        var s1 = ShowOn(1, 4);
        var results = new[]
        {
            R(s1, Jane, Blue, 4, 2),
            R(s1, Bob, Red, 4, 2),
            R(s1, Ann, Red, 5, 1),
        };

        var lines = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider));

        Assert.Equal([1, 1, 3], lines.Select(l => l.Rank).ToArray());
        Assert.Equal(["Bob Jones", "Jane Smith", "Ann Lee"], lines.Select(l => l.Name).ToArray()); // name breaks display order only
    }

    [Fact]
    public void Top_n_shows_counts_only_the_best_shows()
    {
        var s1 = ShowOn(1, 4);
        var s2 = ShowOn(2, 5);
        var s3 = ShowOn(3, 6);
        var results = new[] { R(s1, Jane, Blue, 1, 10), R(s2, Jane, Blue, 2, 6), R(s3, Jane, Blue, 3, 4) };

        var line = Assert.Single(new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider, TopNShows: 2)));

        Assert.Equal(20, line.TotalPoints);
        Assert.Equal(16, line.CountedPoints);
        Assert.Equal(3, line.Shows);
        Assert.False(line.PerShow.Single(s => s.ShowId == 3).Counted);
        Assert.Equal([1, 2, 3], line.PerShow.Select(s => s.ShowId).ToArray()); // breakdown is chronological
    }

    [Fact]
    public void Min_shows_marks_ineligible_and_ranks_them_last()
    {
        var s1 = ShowOn(1, 4);
        var s2 = ShowOn(2, 5);
        var results = new[] { R(s1, Jane, Blue, 1, 10), R(s1, Bob, Red, 2, 6), R(s2, Bob, Red, 2, 6) };

        var lines = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider, MinShows: 2));

        Assert.Equal("Bob Jones", lines[0].Name);
        Assert.True(lines[0].Eligible);
        Assert.Equal("Jane Smith", lines[1].Name);
        Assert.False(lines[1].Eligible);
        Assert.Equal(2, lines[1].Rank);
    }

    [Fact]
    public void Division_filter_limits_results_to_that_division()
    {
        var s1 = ShowOn(1, 4);
        var results = new[] { R(s1, Jane, Blue, 1, 10, Open), R(s1, Bob, Red, 1, 10, Green), R(s1, Jane, Red, 1, 10) };

        var open = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider, DivisionId: Open.Id));
        var overall = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider));

        var line = Assert.Single(open);
        Assert.Equal("Jane Smith", line.Name);
        Assert.Equal(10, line.TotalPoints);
        Assert.Equal(20, overall.Single(l => l.Name == "Jane Smith").TotalPoints);
    }

    [Fact]
    public void Horse_basis_groups_by_horse_and_combination_by_pair()
    {
        var s1 = ShowOn(1, 4);
        var results = new[] { R(s1, Jane, Blue, 1, 10), R(s1, Bob, Blue, 2, 6), R(s1, Jane, Red, 3, 4), R(s1, null, Red, 4, 2) };

        var byHorse = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Horse));
        var byPair = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Combination));
        var byRider = new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider));

        Assert.Equal(["Blue Moon", "Red Sky"], byHorse.Select(l => l.Name).ToArray());
        Assert.Equal(16, byHorse[0].TotalPoints);
        Assert.Equal(6, byHorse[1].TotalPoints); // rider-less result still counts for the horse

        Assert.Equal(3, byPair.Count); // the rider-less result has no pair key
        Assert.Equal("Jane Smith", byPair[0].Name);
        Assert.Equal("Blue Moon", byPair[0].SecondaryName);

        Assert.Equal(2, byRider.Count); // rider-less result is dropped from rider standings
    }

    [Fact]
    public void Unscored_results_count_as_appearances_with_zero_points()
    {
        var s1 = ShowOn(1, 4);
        var results = new[] { R(s1, Jane, Blue, null, null, status: PlacingStatus.Scratched), R(s1, Jane, Blue, 9, 0) };

        var line = Assert.Single(new StandingsCalculator().Compute(results, new StandingsOptions(StandingsBasis.Rider)));

        Assert.Equal(0, line.TotalPoints);
        Assert.Equal(1, line.Shows);
        Assert.Equal(2, line.Classes);
        Assert.Equal(0, line.Firsts);
    }
}
