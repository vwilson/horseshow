using HorseShow.Core.Model;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record DashboardSummary(Season? ActiveSeason, int ShowCount, int ResultCount);

public sealed class DashboardViewService(IDbContextFactory<HorseShowDbContext> factory)
{
    public async Task<Season?> GetActiveSeasonAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive, ct);
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive, ct);
        if (season is null)
        {
            return new DashboardSummary(null, 0, 0);
        }

        var shows = await db.Shows.CountAsync(s => s.SeasonId == season.Id, ct);
        var results = await db.Results.CountAsync(r => r.ShowClass!.Show!.SeasonId == season.Id, ct);
        return new DashboardSummary(season, shows, results);
    }
}
