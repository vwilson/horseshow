using HorseShow.Core.Model;
using HorseShow.Core.Scoring;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record StandingsPage(
    Season Season,
    List<Division> Divisions,
    IReadOnlyList<StandingLine> Lines,
    int UnassignedClasses,
    int FallbackEntryCountClasses,
    bool HasSchedule);

public sealed class StandingsViewService(IDbContextFactory<HorseShowDbContext> factory)
{
    private readonly StandingsCalculator _calculator = new();

    public async Task<StandingsPage?> GetAsync(int seasonId, StandingsBasis? basis, int? divisionId, CancellationToken ct = default)
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

        var options = new StandingsOptions(basis ?? season.StandingsBasis, divisionId, season.TopNShowsCounted, season.MinShowsForAward);
        var lines = _calculator.Compute(results, options);

        var classes = await db.ShowClasses.AsNoTracking()
            .Where(c => c.Show!.SeasonId == seasonId)
            .Select(c => new { c.DivisionId, c.EntryCount, HasResults = c.Results.Any() })
            .ToListAsync(ct);

        var hasSchedule = season.DefaultPointScheduleId is not null || season.Divisions.Any(d => d.PointScheduleId is not null);
        return new StandingsPage(
            season,
            season.Divisions.ToList(),
            lines,
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
