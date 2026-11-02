using HorseShow.Core.Model;
using HorseShow.Core.Scoring;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Data;

public sealed record PointsChange(int ResultId, string ShowName, string ClassName, string Competitor, int? Placing, decimal? OldPoints, decimal? NewPoints);

/// <summary>
/// Applies <see cref="ResultScoring"/> to stored results. Only results with
/// <see cref="PointsSource.Computed"/> are touched; imported and manual points are left alone.
/// </summary>
public static class PointsRecalculator
{
    /// <summary>Recomputes one class inside the caller's context. Changes are tracked, not saved.</summary>
    public static async Task<int> RecalculateClassAsync(HorseShowDbContext db, int classId, CancellationToken ct = default)
    {
        var cls = await db.ShowClasses
            .Include(c => c.Show).ThenInclude(s => s!.Season).ThenInclude(s => s!.DefaultPointSchedule).ThenInclude(p => p!.Rows)
            .Include(c => c.Division).ThenInclude(d => d!.PointSchedule).ThenInclude(p => p!.Rows)
            .Include(c => c.Results)
            .FirstOrDefaultAsync(c => c.Id == classId, ct);
        if (cls is null)
        {
            return 0;
        }

        return ApplyToClass(cls);
    }

    /// <summary>Lists what a season-wide recalculation would change, without changing anything.</summary>
    public static async Task<List<PointsChange>> PreviewSeasonAsync(HorseShowDbContext db, int seasonId, CancellationToken ct = default)
    {
        var classes = await LoadSeasonClassesAsync(db.ShowClasses.AsNoTracking(), seasonId, ct);
        var changes = new List<PointsChange>();
        foreach (var cls in classes)
        {
            var schedule = ResultScoring.ScheduleFor(cls.Show!.Season!, cls.Division);
            var entries = ResultScoring.EffectiveEntryCount(cls);
            foreach (var r in cls.Results.Where(r => r.PointsSource == PointsSource.Computed))
            {
                var next = ResultScoring.ComputePoints(schedule, r, entries);
                if (next != r.Points)
                {
                    changes.Add(new PointsChange(r.Id, cls.Show.Name, cls.Name, Competitor(r), r.Placing, r.Points, next));
                }
            }
        }

        return changes;
    }

    /// <summary>Recomputes every computed result in the season and saves. Returns the number changed.</summary>
    public static async Task<int> RecalculateSeasonAsync(HorseShowDbContext db, int seasonId, CancellationToken ct = default)
    {
        var classes = await LoadSeasonClassesAsync(db.ShowClasses, seasonId, ct);
        var changed = classes.Sum(ApplyToClass);
        await db.SaveChangesAsync(ct);
        return changed;
    }

    private static Task<List<ShowClass>> LoadSeasonClassesAsync(IQueryable<ShowClass> query, int seasonId, CancellationToken ct)
        => query
            .Where(c => c.Show!.SeasonId == seasonId)
            .Include(c => c.Show).ThenInclude(s => s!.Season).ThenInclude(s => s!.DefaultPointSchedule).ThenInclude(p => p!.Rows)
            .Include(c => c.Division).ThenInclude(d => d!.PointSchedule).ThenInclude(p => p!.Rows)
            .Include(c => c.Results).ThenInclude(r => r.Rider)
            .Include(c => c.Results).ThenInclude(r => r.Horse)
            .ToListAsync(ct);

    private static int ApplyToClass(ShowClass cls)
    {
        var schedule = ResultScoring.ScheduleFor(cls.Show!.Season!, cls.Division);
        var entries = ResultScoring.EffectiveEntryCount(cls);
        var changed = 0;
        foreach (var r in cls.Results.Where(r => r.PointsSource == PointsSource.Computed))
        {
            var next = ResultScoring.ComputePoints(schedule, r, entries);
            if (next != r.Points)
            {
                r.Points = next;
                changed++;
            }
        }

        return changed;
    }

    private static string Competitor(Result r)
    {
        var rider = r.Rider?.DisplayName ?? r.Rider?.Name;
        var horse = r.Horse?.Name;
        return (rider, horse) switch
        {
            (not null, not null) => $"{rider} / {horse}",
            (not null, null) => rider,
            (null, not null) => horse,
            _ => $"#{r.Id}",
        };
    }
}
