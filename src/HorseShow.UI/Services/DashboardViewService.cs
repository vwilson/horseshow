using HorseShow.Core.Model;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record DashboardSummary(Season? ActiveSeason, int ShowCount, int ResultCount, int RiderCount, int HorseCount, List<ShowRow> RecentShows);

public sealed class DashboardViewService(IDbContextFactory<HorseShowDbContext> factory, ActiveSeasonService active)
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var season = await active.GetAsync(ct);
        if (season is null)
        {
            return new DashboardSummary(null, 0, 0, 0, 0, []);
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        var shows = await db.Shows.CountAsync(s => s.SeasonId == season.Id, ct);
        var seasonResults = db.Results.Where(r => r.ShowClass!.Show!.SeasonId == season.Id);
        var results = await seasonResults.CountAsync(ct);
        var riders = await seasonResults.Where(r => r.RiderId != null).Select(r => r.RiderId).Distinct().CountAsync(ct);
        var horses = await seasonResults.Where(r => r.HorseId != null).Select(r => r.HorseId).Distinct().CountAsync(ct);
        var recent = await db.Shows.AsNoTracking()
            .Where(s => s.SeasonId == season.Id)
            .OrderByDescending(s => s.StartDate)
            .Take(5)
            .Select(s => new ShowRow(s, s.Classes.Count, s.Classes.SelectMany(c => c.Results).Count()))
            .ToListAsync(ct);

        return new DashboardSummary(season, shows, results, riders, horses, recent);
    }
}
