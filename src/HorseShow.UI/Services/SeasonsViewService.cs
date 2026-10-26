using HorseShow.Core.Model;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record SeasonRow(Season Season, int ShowCount, int ResultCount);

public sealed class SeasonsViewService(IDbContextFactory<HorseShowDbContext> factory, ActiveSeasonService active)
{
    public async Task<List<SeasonRow>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Seasons.AsNoTracking()
            .OrderByDescending(s => s.Year).ThenBy(s => s.Name)
            .Select(s => new SeasonRow(
                s,
                s.Shows.Count,
                s.Shows.SelectMany(sh => sh.Classes).SelectMany(c => c.Results).Count()))
            .ToListAsync(ct);
    }

    public async Task<Season?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Seasons.AsNoTracking()
            .Include(s => s.DefaultPointSchedule)
            .Include(s => s.Divisions.OrderBy(d => d.SortOrder).ThenBy(d => d.Name)).ThenInclude(d => d.PointSchedule)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Season> SaveAsync(SeasonEdit edit, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Season season;
        if (edit.Id == 0)
        {
            season = new Season { Name = edit.Name };
            // First season ever becomes active so the dashboard has something to show.
            season.IsActive = !await db.Seasons.AnyAsync(ct);
            db.Seasons.Add(season);
        }
        else
        {
            season = await db.Seasons.FirstAsync(s => s.Id == edit.Id, ct);
        }

        edit.ApplyTo(season);
        await db.SaveChangesAsync(ct);
        if (season.IsActive)
        {
            await active.RefreshAsync(ct);
        }

        return season;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (season is null)
        {
            return;
        }

        db.Seasons.Remove(season);
        await db.SaveChangesAsync(ct);
        if (season.IsActive)
        {
            await active.RefreshAsync(ct);
        }
    }

    /// <summary>Built-in schedules plus any defined for the given season.</summary>
    public async Task<List<PointSchedule>> ListPointSchedulesAsync(int? seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.PointSchedules.AsNoTracking()
            .Where(p => p.SeasonId == null || p.SeasonId == seasonId)
            .OrderBy(p => p.IsBuiltIn ? 1 : 0).ThenBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Division> SaveDivisionAsync(DivisionEdit edit, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var duplicate = await db.Divisions.AnyAsync(
            d => d.SeasonId == edit.SeasonId && d.Id != edit.Id && d.Name == edit.Name.Trim(), ct);
        if (duplicate)
        {
            throw new InvalidOperationException($"A division named \"{edit.Name.Trim()}\" already exists in this season.");
        }

        Division division;
        if (edit.Id == 0)
        {
            division = new Division { SeasonId = edit.SeasonId, Name = edit.Name };
            if (edit.SortOrder == 0)
            {
                var max = await db.Divisions.Where(d => d.SeasonId == edit.SeasonId).MaxAsync(d => (int?)d.SortOrder, ct) ?? 0;
                edit.SortOrder = max + 10;
            }

            db.Divisions.Add(division);
        }
        else
        {
            division = await db.Divisions.FirstAsync(d => d.Id == edit.Id, ct);
        }

        edit.ApplyTo(division);
        await db.SaveChangesAsync(ct);
        return division;
    }

    public async Task DeleteDivisionAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Divisions.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
    }
}
