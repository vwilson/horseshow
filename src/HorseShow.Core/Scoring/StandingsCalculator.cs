using HorseShow.Core.Model;

namespace HorseShow.Core.Scoring;

public sealed record StandingsOptions(
    StandingsBasis Basis,
    int? DivisionId = null,
    int? TopNShows = null,
    int? MinShows = null,
    /// <summary>Only results whose rider is in this group. Empty string means riders with no group.</summary>
    string? RiderGroup = null);

/// <summary>One show's contribution to a competitor's line.</summary>
public sealed record ShowPoints(int ShowId, string ShowName, DateOnly Date, decimal Points, int Classes, bool Counted);

public sealed class StandingLine
{
    public required string Key { get; init; }
    public int? RiderId { get; init; }
    public int? HorseId { get; init; }
    public required string Name { get; init; }
    public string? SecondaryName { get; init; }
    /// <summary>The rider's group at the time of computation, for export and sectioned views.</summary>
    public string? RiderGroup { get; init; }

    public int Rank { get; set; }
    public decimal TotalPoints { get; init; }
    /// <summary>Points after the top-N rule. Equals TotalPoints when no rule is set.</summary>
    public decimal CountedPoints { get; init; }
    public int Shows { get; init; }
    public int Classes { get; init; }
    public int Firsts { get; init; }
    public int Seconds { get; init; }
    public int Thirds { get; init; }
    /// <summary>False when the competitor is below the minimum-shows rule. Still listed, ranked last.</summary>
    public bool Eligible { get; init; } = true;
    public IReadOnlyList<ShowPoints> PerShow { get; init; } = [];
}

/// <summary>
/// Aggregates stored result points into season standings. Pure; takes results with
/// ShowClass, Show, Division, Rider and Horse loaded.
/// </summary>
public sealed class StandingsCalculator
{
    public IReadOnlyList<StandingLine> Compute(IEnumerable<Result> results, StandingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(options);

        var scoped = results.Where(r => r.ShowClass?.Show is not null);
        if (options.DivisionId is int divisionId)
        {
            scoped = scoped.Where(r => r.ShowClass!.DivisionId == divisionId);
        }

        if (options.RiderGroup is { } wantedGroup)
        {
            scoped = wantedGroup.Length == 0
                ? scoped.Where(r => r.Rider is not null && string.IsNullOrWhiteSpace(r.Rider.Group))
                : scoped.Where(r => string.Equals(r.Rider?.Group?.Trim(), wantedGroup.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var lines = new List<StandingLine>();
        foreach (var group in scoped.Select(r => (Key: KeyFor(r, options.Basis), Result: r)).Where(x => x.Key is not null).GroupBy(x => x.Key!))
        {
            var items = group.Select(x => x.Result).ToList();
            var first = items[0];

            var perShow = items
                .GroupBy(r => r.ShowClass!.Show!)
                .Select(g => new ShowPoints(g.Key.Id, g.Key.Name, g.Key.StartDate, g.Sum(r => r.Points ?? 0m), g.Count(), Counted: true))
                .OrderByDescending(s => s.Points).ThenBy(s => s.Date)
                .ToList();

            if (options.TopNShows is int topN && topN > 0 && perShow.Count > topN)
            {
                perShow = perShow.Select((s, i) => s with { Counted = i < topN }).ToList();
            }

            var total = perShow.Sum(s => s.Points);
            var counted = perShow.Where(s => s.Counted).Sum(s => s.Points);
            var eligible = options.MinShows is not int min || perShow.Count >= min;

            lines.Add(new StandingLine
            {
                Key = group.Key,
                RiderId = options.Basis != StandingsBasis.Horse ? first.RiderId : null,
                HorseId = options.Basis != StandingsBasis.Rider ? first.HorseId : null,
                Name = NameFor(first, options.Basis),
                SecondaryName = options.Basis == StandingsBasis.Combination ? HorseName(first) : null,
                RiderGroup = options.Basis == StandingsBasis.Horse ? null : first.Rider?.Group,
                TotalPoints = total,
                CountedPoints = counted,
                Shows = perShow.Count,
                Classes = items.Count,
                Firsts = items.Count(r => r.PlacingStatus == PlacingStatus.Placed && r.Placing == 1),
                Seconds = items.Count(r => r.PlacingStatus == PlacingStatus.Placed && r.Placing == 2),
                Thirds = items.Count(r => r.PlacingStatus == PlacingStatus.Placed && r.Placing == 3),
                Eligible = eligible,
                PerShow = perShow.OrderBy(s => s.Date).ThenBy(s => s.ShowName).ToList(),
            });
        }

        var ordered = lines
            .OrderByDescending(l => l.Eligible)
            .ThenByDescending(l => l.CountedPoints)
            .ThenByDescending(l => l.Firsts)
            .ThenByDescending(l => l.Seconds)
            .ThenByDescending(l => l.Thirds)
            .ThenByDescending(l => l.Shows)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Competition ranking: equal lines share a rank, the next rank skips (1, 1, 3).
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Rank = i > 0 && SameRank(ordered[i - 1], ordered[i]) ? ordered[i - 1].Rank : i + 1;
        }

        return ordered;
    }

    private static bool SameRank(StandingLine a, StandingLine b)
        => a.Eligible == b.Eligible && a.CountedPoints == b.CountedPoints && a.Firsts == b.Firsts && a.Seconds == b.Seconds && a.Thirds == b.Thirds;

    private static string? KeyFor(Result r, StandingsBasis basis) => basis switch
    {
        StandingsBasis.Rider => r.RiderId is int rid ? $"r{rid}" : null,
        StandingsBasis.Horse => r.HorseId is int hid ? $"h{hid}" : null,
        StandingsBasis.Combination => r.RiderId is int rid2 && r.HorseId is int hid2 ? $"r{rid2}h{hid2}" : null,
        _ => null,
    };

    private static string NameFor(Result r, StandingsBasis basis) => basis switch
    {
        StandingsBasis.Horse => HorseName(r),
        _ => r.Rider?.DisplayName ?? r.Rider?.Name ?? $"Rider {r.RiderId}",
    };

    private static string HorseName(Result r) => r.Horse?.Name ?? $"Horse {r.HorseId}";
}
