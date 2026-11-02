using HorseShow.Core.Model;
using HorseShow.Core.Scoring;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record StandingsSection(string Title, string? RiderGroup, IReadOnlyList<StandingLine> Lines);

public sealed record StandingsPage(
    Season Season,
    List<Division> Divisions,
    IReadOnlyList<StandingLine> Lines,
    IReadOnlyList<StandingsSection> Sections,
    List<string> RiderGroups,
    int UngroupedRiders,
    int UnassignedClasses,
    int FallbackEntryCountClasses,
    bool HasSchedule);

public sealed class StandingsViewService(IDbContextFactory<HorseShowDbContext> factory)
{
    private readonly StandingsCalculator _calculator = new();

    /// <param name="riderGroup">Null: all riders. Empty string: riders with no group. Otherwise one group.</param>
    /// <param name="sectionByGroup">When true, <see cref="StandingsPage.Sections"/> holds one independently ranked table per rider group.</param>
    public async Task<StandingsPage?> GetAsync(int seasonId, StandingsBasis? basis, int? divisionId, string? riderGroup = null, bool sectionByGroup = false, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var season = await db.Seasons.AsNoTracking()
            .Include(s => s.Divisions.OrderBy(d => d.SortOrder).ThenBy(d => d.Name)).ThenInclude(d => d.PointSchedule)
            .Include(s => s.DefaultPointSchedule)
            .FirstOrDefaultAsync(s => s.Id == seasonId, ct);
        if (season is null)
        {
            return null;
        }

        var results = await db.Results.AsNoTracking()
            .Where(r => r.ShowClass!.Show!.SeasonId == seasonId)
            .Include(r => r.ShowClass).ThenInclude(c => c!.Show)
            .Include(r => r.ShowClass).ThenInclude(c => c!.Division)
            .Include(r => r.Rider)
            .Include(r => r.Horse)
            .ToListAsync(ct);

        var effectiveBasis = basis ?? season.StandingsBasis;
        var options = new StandingsOptions(effectiveBasis, divisionId, season.TopNShowsCounted, season.MinShowsForAward, riderGroup);
        var lines = _calculator.Compute(results, options);

        var groups = results.Select(r => r.Rider?.Group?.Trim()).Where(g => !string.IsNullOrEmpty(g))
            .GroupBy(g => g!, StringComparer.OrdinalIgnoreCase).Select(g => g.Key)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList();
        var ungrouped = results.Where(r => r.RiderId is not null && string.IsNullOrWhiteSpace(r.Rider?.Group)).Select(r => r.RiderId).Distinct().Count();

        var sections = new List<StandingsSection>();
        if (sectionByGroup && effectiveBasis != StandingsBasis.Horse)
        {
            foreach (var g in groups)
            {
                sections.Add(new StandingsSection(g, g, _calculator.Compute(results, options with { RiderGroup = g })));
            }

            if (ungrouped > 0)
            {
                sections.Add(new StandingsSection("No group", string.Empty, _calculator.Compute(results, options with { RiderGroup = string.Empty })));
            }
        }

        var classes = await db.ShowClasses.AsNoTracking()
            .Where(c => c.Show!.SeasonId == seasonId)
            .Select(c => new { c.DivisionId, c.EntryCount, HasResults = c.Results.Any() })
            .ToListAsync(ct);

        var hasSchedule = season.DefaultPointScheduleId is not null || season.Divisions.Any(d => d.PointScheduleId is not null);
        return new StandingsPage(
            season,
            season.Divisions.ToList(),
            lines,
            sections,
            groups,
            ungrouped,
            classes.Count(c => c.DivisionId is null && c.HasResults),
            classes.Count(c => c.EntryCount is null && c.HasResults),
            hasSchedule);
    }

    public async Task<List<PointsChange>> PreviewRecalculationAsync(int seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PointsRecalculator.PreviewSeasonAsync(db, seasonId, ct);
    }

    public async Task<int> RecalculateAsync(int seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PointsRecalculator.RecalculateSeasonAsync(db, seasonId, ct);
    }
}
