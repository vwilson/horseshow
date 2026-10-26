using HorseShow.Core.Model;
using HorseShow.Core.Text;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record RiderRow(Rider Rider, int ResultCount);
public sealed record HorseRow(Horse Horse, int ResultCount);

public sealed class ParticipantsViewService(IDbContextFactory<HorseShowDbContext> factory)
{
    private const int OptionLimit = 12;

    // ---- Riders ----

    public async Task<List<RiderRow>> ListRidersAsync(bool includeArchived, string? search, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var q = db.Riders.AsNoTracking().Include(r => r.Aliases).AsQueryable();
        if (!includeArchived)
        {
            q = q.Where(r => !r.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = NameNormalizer.Normalize(search);
            var raw = search.Trim();
            q = q.Where(r => r.Aliases.Any(a => a.Alias.Contains(key)) || r.Name.Contains(raw) || (r.Barn != null && r.Barn.Contains(raw)));
        }

        return await q.OrderBy(r => r.Name)
            .Select(r => new RiderRow(r, db.Results.Count(x => x.RiderId == r.Id)))
            .ToListAsync(ct);
    }

    public async Task<Rider> SaveRiderAsync(RiderEdit edit, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Rider rider;
        if (edit.Id == 0)
        {
            rider = new Rider { Name = edit.Name };
            db.Riders.Add(rider);
        }
        else
        {
            rider = await db.Riders.Include(r => r.Aliases).FirstAsync(r => r.Id == edit.Id, ct);
        }

        rider.Name = edit.Name.Trim();
        rider.DisplayName = Clean(edit.DisplayName);
        rider.Barn = Clean(edit.Barn);
        rider.Notes = Clean(edit.Notes);

        var wanted = NormalizedAliasSet(edit.Aliases, rider.Name);
        var taken = await db.RiderAliases.AsNoTracking()
            .Where(a => wanted.Contains(a.Alias) && a.RiderId != rider.Id)
            .Include(a => a.Rider)
            .FirstOrDefaultAsync(ct);
        if (taken is not null)
        {
            throw new InvalidOperationException($"\"{taken.Alias}\" already belongs to rider {taken.Rider!.Name}.");
        }

        SyncAliases(rider.Aliases, wanted, a => a.Alias, alias => new RiderAlias { Alias = alias });
        await db.SaveChangesAsync(ct);
        return rider;
    }

    public async Task SetRiderArchivedAsync(int id, bool archived, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Riders.Where(r => r.Id == id).ExecuteUpdateAsync(u => u.SetProperty(r => r.IsArchived, archived), ct);
    }

    public async Task DeleteRiderAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Results.AnyAsync(r => r.RiderId == id, ct))
        {
            throw new InvalidOperationException("This rider has results. Archive them instead of deleting.");
        }

        await db.Riders.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<IEnumerable<NameOption>> SearchRiderOptionsAsync(string? text, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await SearchOptionsAsync(
            text,
            key => db.RiderAliases.Where(a => a.Alias.Contains(key) && !a.Rider!.IsArchived)
                .Select(a => new { a.Rider!.Id, Name = a.Rider.DisplayName ?? a.Rider.Name, a.Alias })
                .Take(OptionLimit * 3)
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.Name, x.Alias)), ct),
            ct);
    }

    // ---- Horses ----

    public async Task<List<HorseRow>> ListHorsesAsync(bool includeArchived, string? search, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var q = db.Horses.AsNoTracking().Include(h => h.Aliases).AsQueryable();
        if (!includeArchived)
        {
            q = q.Where(h => !h.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = NameNormalizer.Normalize(search);
            var raw = search.Trim();
            q = q.Where(h => h.Aliases.Any(a => a.Alias.Contains(key)) || h.Name.Contains(raw) || (h.Owner != null && h.Owner.Contains(raw)));
        }

        return await q.OrderBy(h => h.Name)
            .Select(h => new HorseRow(h, db.Results.Count(x => x.HorseId == h.Id)))
            .ToListAsync(ct);
    }

    public async Task<Horse> SaveHorseAsync(HorseEdit edit, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Horse horse;
        if (edit.Id == 0)
        {
            horse = new Horse { Name = edit.Name };
            db.Horses.Add(horse);
        }
        else
        {
            horse = await db.Horses.Include(h => h.Aliases).FirstAsync(h => h.Id == edit.Id, ct);
        }

        horse.Name = edit.Name.Trim();
        horse.Owner = Clean(edit.Owner);
        horse.RegistrationNumber = Clean(edit.RegistrationNumber);
        horse.Notes = Clean(edit.Notes);

        var wanted = NormalizedAliasSet(edit.Aliases, horse.Name);
        var taken = await db.HorseAliases.AsNoTracking()
            .Where(a => wanted.Contains(a.Alias) && a.HorseId != horse.Id)
            .Include(a => a.Horse)
            .FirstOrDefaultAsync(ct);
        if (taken is not null)
        {
            throw new InvalidOperationException($"\"{taken.Alias}\" already belongs to horse {taken.Horse!.Name}.");
        }

        SyncAliases(horse.Aliases, wanted, a => a.Alias, alias => new HorseAlias { Alias = alias });
        await db.SaveChangesAsync(ct);
        return horse;
    }

    public async Task SetHorseArchivedAsync(int id, bool archived, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Horses.Where(h => h.Id == id).ExecuteUpdateAsync(u => u.SetProperty(h => h.IsArchived, archived), ct);
    }

    public async Task DeleteHorseAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Results.AnyAsync(r => r.HorseId == id, ct))
        {
            throw new InvalidOperationException("This horse has results. Archive it instead of deleting.");
        }

        await db.Horses.Where(h => h.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<IEnumerable<NameOption>> SearchHorseOptionsAsync(string? text, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await SearchOptionsAsync(
            text,
            key => db.HorseAliases.Where(a => a.Alias.Contains(key) && !a.Horse!.IsArchived)
                .Select(a => new { a.Horse!.Id, a.Horse.Name, a.Alias })
                .Take(OptionLimit * 3)
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.Name, x.Alias)), ct),
            ct);
    }

    // ---- shared ----

    /// <summary>
    /// Existing matches by alias, de-duplicated per entity, followed by a "create" option when
    /// the typed text does not exactly match an existing alias.
    /// </summary>
    private static async Task<IEnumerable<NameOption>> SearchOptionsAsync(
        string? text,
        Func<string, Task<IEnumerable<(int Id, string Name, string Alias)>>> query,
        CancellationToken ct)
    {
        var typed = text?.Trim() ?? string.Empty;
        if (typed.Length == 0)
        {
            return [];
        }

        var key = NameNormalizer.Normalize(typed);
        var hits = (await query(key)).ToList();

        var options = hits
            .GroupBy(h => h.Id)
            .Select(g => new NameOption(g.Key, g.First().Name))
            .OrderBy(o => o.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(o => o.Name)
            .Take(OptionLimit)
            .ToList();

        if (!hits.Any(h => h.Alias == key))
        {
            options.Add(new NameOption(null, typed));
        }

        return options;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static HashSet<string> NormalizedAliasSet(IEnumerable<string> aliases, string name)
    {
        var set = aliases.Select(NameNormalizer.Normalize).Where(a => a.Length > 0).ToHashSet();
        set.Add(NameNormalizer.Normalize(name));
        return set;
    }

    private static void SyncAliases<TAlias>(ICollection<TAlias> current, HashSet<string> wanted, Func<TAlias, string> key, Func<string, TAlias> create)
    {
        foreach (var stale in current.Where(a => !wanted.Contains(key(a))).ToList())
        {
            current.Remove(stale);
        }

        var have = current.Select(key).ToHashSet();
        foreach (var alias in wanted.Where(a => !have.Contains(a)))
        {
            current.Add(create(alias));
        }
    }
}
