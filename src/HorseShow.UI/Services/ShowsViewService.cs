using HorseShow.Core.Model;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record ShowRow(Show Show, int ClassCount, int ResultCount);

public sealed class ShowsViewService(IDbContextFactory<HorseShowDbContext> factory)
{
    public async Task<List<ShowRow>> ListAsync(int seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Shows.AsNoTracking()
            .Where(s => s.SeasonId == seasonId)
            .OrderByDescending(s => s.StartDate).ThenBy(s => s.Name)
            .Select(s => new ShowRow(s, s.Classes.Count, s.Classes.SelectMany(c => c.Results).Count()))
            .ToListAsync(ct);
    }

    public async Task<Show?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Shows.AsNoTracking()
            .Include(s => s.Season)
            .Include(s => s.Classes.OrderBy(c => c.SortOrder).ThenBy(c => c.Id)).ThenInclude(c => c.Division)
            .Include(s => s.Classes).ThenInclude(c => c.Results)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Show> SaveShowAsync(ShowEdit edit, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Show show;
        if (edit.Id == 0)
        {
            show = new Show { Name = edit.Name, SeasonId = edit.SeasonId };
            db.Shows.Add(show);
        }
        else
        {
            show = await db.Shows.FirstAsync(s => s.Id == edit.Id, ct);
        }

        edit.ApplyTo(show);
        await db.SaveChangesAsync(ct);
        return show;
    }

    public async Task DeleteShowAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Shows.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<ShowClass> SaveClassAsync(ShowClassEdit edit, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        ShowClass cls;
        if (edit.Id == 0)
        {
            cls = new ShowClass { Name = edit.Name, ShowId = edit.ShowId };
            var max = await db.ShowClasses.Where(c => c.ShowId == edit.ShowId).MaxAsync(c => (int?)c.SortOrder, ct) ?? 0;
            cls.SortOrder = max + 10;
            db.ShowClasses.Add(cls);
        }
        else
        {
            cls = await db.ShowClasses.FirstAsync(c => c.Id == edit.Id, ct);
        }

        edit.ApplyTo(cls);
        await db.SaveChangesAsync(ct);
        return cls;
    }

    public async Task DeleteClassAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.ShowClasses.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<List<ResultRow>> GetClassResultsAsync(int classId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var results = await db.Results.AsNoTracking()
            .Include(r => r.Rider).Include(r => r.Horse)
            .Where(r => r.ShowClassId == classId)
            .ToListAsync(ct);

        return results
            .OrderBy(r => r.PlacingStatus == PlacingStatus.Placed ? 0 : 1)
            .ThenBy(r => r.Placing ?? int.MaxValue)
            .ThenBy(r => r.Id)
            .Select(ResultRow.From)
            .ToList();
    }

    /// <summary>
    /// Creates or updates a result. Rider/horse options with a null Id are created on the fly
    /// through <see cref="ParticipantLookup"/>, so typing a new name in the grid is enough.
    /// </summary>
    public async Task<ResultRow> SaveResultAsync(ResultRow row, CancellationToken ct = default)
    {
        if (row.Rider is null && row.Horse is null)
        {
            throw new InvalidOperationException("A result needs a rider or a horse.");
        }

        if (row.Status == PlacingStatus.Placed && row.Placing is null or < 1)
        {
            throw new InvalidOperationException("A placed result needs a placing of 1 or higher.");
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        Result result;
        if (row.Id == 0)
        {
            result = new Result { ShowClassId = row.ShowClassId };
            db.Results.Add(result);
        }
        else
        {
            result = await db.Results.FirstAsync(r => r.Id == row.Id, ct);
        }

        result.Placing = row.Status == PlacingStatus.Placed ? row.Placing : null;
        result.PlacingStatus = row.Status;
        result.BackNumber = string.IsNullOrWhiteSpace(row.BackNumber) ? null : row.BackNumber.Trim();
        result.Score = row.Score;

        if (row.Rider is null)
        {
            result.Rider = null;
            result.RiderId = null;
        }
        else
        {
            result.Rider = row.Rider.Id is int riderId
                ? await db.Riders.FirstAsync(r => r.Id == riderId, ct)
                : await ParticipantLookup.GetOrCreateRiderAsync(db, row.Rider.Name, ct);
        }

        if (row.Horse is null)
        {
            result.Horse = null;
            result.HorseId = null;
        }
        else
        {
            result.Horse = row.Horse.Id is int horseId
                ? await db.Horses.FirstAsync(h => h.Id == horseId, ct)
                : await ParticipantLookup.GetOrCreateHorseAsync(db, row.Horse.Name, ct);
        }

        if (row.PointsSource == PointsSource.Manual)
        {
            result.Points = row.Points;
            result.PointsSource = PointsSource.Manual;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ResultRow.From(result);
    }

    public async Task DeleteResultAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Results.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }
}
