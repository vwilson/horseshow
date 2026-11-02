using HorseShow.Core.Model;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

/// <summary>Scoped per circuit/window. Caches the active season and tells the layout when it changes.</summary>
public sealed class ActiveSeasonService(IDbContextFactory<HorseShowDbContext> factory)
{
    private bool _loaded;

    public event Action? Changed;

    public Season? Current { get; private set; }

    public async Task<Season?> GetAsync(CancellationToken ct = default)
    {
        if (!_loaded)
        {
            await RefreshAsync(ct);
        }

        return Current;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Current = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive, ct);
        _loaded = true;
        Changed?.Invoke();
    }

    public async Task SetActiveAsync(int seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Seasons.Where(s => s.IsActive && s.Id != seasonId).ExecuteUpdateAsync(u => u.SetProperty(s => s.IsActive, false), ct);
        await db.Seasons.Where(s => s.Id == seasonId).ExecuteUpdateAsync(u => u.SetProperty(s => s.IsActive, true), ct);
        await tx.CommitAsync(ct);
        await RefreshAsync(ct);
    }
}
