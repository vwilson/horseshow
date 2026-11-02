using HorseShow.Core.Model;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record ScheduleRow(PointSchedule Schedule, int RowCount, int UsedBySeasons, int UsedByDivisions);

public sealed class SchedulesViewService(IDbContextFactory<HorseShowDbContext> factory)
{
    /// <summary>Built-in schedules plus those belonging to <paramref name="seasonId"/>.</summary>
    public async Task<List<ScheduleRow>> ListAsync(int? seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.PointSchedules.AsNoTracking()
            .Where(p => p.SeasonId == null || p.SeasonId == seasonId)
            .OrderBy(p => p.IsBuiltIn ? 1 : 0).ThenBy(p => p.Name)
            .Select(p => new ScheduleRow(
                p,
                p.Rows.Count,
                db.Seasons.Count(s => s.DefaultPointScheduleId == p.Id),
                db.Divisions.Count(d => d.PointScheduleId == p.Id)))
            .ToListAsync(ct);
    }

    public async Task<PointSchedule?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.PointSchedules.AsNoTracking().Include(p => p.Rows).FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<PointSchedule> SaveAsync(PointScheduleEdit edit, CancellationToken ct = default)
    {
        Validate(edit);

        await using var db = await factory.CreateDbContextAsync(ct);
        PointSchedule schedule;
        if (edit.Id == 0)
        {
            schedule = new PointSchedule { Name = edit.Name, SeasonId = edit.SeasonId };
            db.PointSchedules.Add(schedule);
        }
        else
        {
            schedule = await db.PointSchedules.Include(p => p.Rows).FirstAsync(p => p.Id == edit.Id, ct);
            if (schedule.IsBuiltIn)
            {
                throw new InvalidOperationException("Built-in schedules are read-only. Clone it to make changes.");
            }
        }

        schedule.Name = edit.Name.Trim();
        schedule.Mode = edit.Mode;
        schedule.Rows.Clear();
        foreach (var row in edit.Rows)
        {
            schedule.Rows.Add(new PointScheduleRow
            {
                Placing = row.Placing,
                Points = row.Points,
                MinEntries = edit.Mode == PointScheduleMode.Fixed ? 0 : row.MinEntries,
            });
        }

        await db.SaveChangesAsync(ct);

        // Every season that scores under this schedule, directly or through a division, needs new points.
        var seasonIds = await db.Seasons.Where(s => s.DefaultPointScheduleId == schedule.Id).Select(s => s.Id)
            .Union(db.Divisions.Where(d => d.PointScheduleId == schedule.Id).Select(d => d.SeasonId))
            .ToListAsync(ct);
        foreach (var seasonId in seasonIds)
        {
            await PointsRecalculator.RecalculateSeasonAsync(db, seasonId, ct);
        }

        return schedule;
    }

    public async Task<PointSchedule> CloneAsync(int id, int? seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var source = await db.PointSchedules.AsNoTracking().Include(p => p.Rows).FirstAsync(p => p.Id == id, ct);
        var copy = new PointSchedule { Name = $"{source.Name} (copy)", Mode = source.Mode, SeasonId = seasonId, IsBuiltIn = false };
        foreach (var r in source.Rows)
        {
            copy.Rows.Add(new PointScheduleRow { Placing = r.Placing, Points = r.Points, MinEntries = r.MinEntries });
        }

        db.PointSchedules.Add(copy);
        await db.SaveChangesAsync(ct);
        return copy;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var schedule = await db.PointSchedules.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (schedule is null)
        {
            return;
        }

        if (schedule.IsBuiltIn)
        {
            throw new InvalidOperationException("Built-in schedules cannot be deleted.");
        }

        var seasonIds = await db.Seasons.Where(s => s.DefaultPointScheduleId == id).Select(s => s.Id)
            .Union(db.Divisions.Where(d => d.PointScheduleId == id).Select(d => d.SeasonId))
            .ToListAsync(ct);

        db.PointSchedules.Remove(schedule);
        await db.SaveChangesAsync(ct);

        foreach (var seasonId in seasonIds)
        {
            await PointsRecalculator.RecalculateSeasonAsync(db, seasonId, ct);
        }
    }

    private static void Validate(PointScheduleEdit edit)
    {
        if (string.IsNullOrWhiteSpace(edit.Name))
        {
            throw new InvalidOperationException("The schedule needs a name.");
        }

        if (edit.Rows.Count == 0)
        {
            throw new InvalidOperationException("Add at least one row.");
        }

        if (edit.Rows.Any(r => r.Placing < 1))
        {
            throw new InvalidOperationException("Placings start at 1.");
        }

        if (edit.Rows.Any(r => r.Points < 0))
        {
            throw new InvalidOperationException("Points cannot be negative.");
        }

        var keys = edit.Rows.Select(r => (r.Placing, edit.Mode == PointScheduleMode.Fixed ? 0 : r.MinEntries)).ToList();
        if (keys.Distinct().Count() != keys.Count)
        {
            throw new InvalidOperationException(edit.Mode == PointScheduleMode.Fixed
                ? "Each placing can appear only once."
                : "Each placing can appear only once per entry tier.");
        }
    }
}
