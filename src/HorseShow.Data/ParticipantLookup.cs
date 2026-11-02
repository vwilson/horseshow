using HorseShow.Core.Model;
using HorseShow.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Data;

/// <summary>
/// Resolves a typed or imported name to a rider/horse through the alias tables, creating the
/// entity (and its canonical alias) when nothing matches. Works inside the caller's context so
/// it participates in the caller's transaction. Shared by manual entry and the importer.
/// </summary>
public static class ParticipantLookup
{
    public static async Task<Rider?> FindRiderAsync(HorseShowDbContext db, string name, CancellationToken ct = default)
    {
        var key = NameNormalizer.Normalize(name);
        if (key.Length == 0)
        {
            return null;
        }

        return await db.RiderAliases.Where(a => a.Alias == key).Select(a => a.Rider).FirstOrDefaultAsync(ct);
    }

    public static async Task<Horse?> FindHorseAsync(HorseShowDbContext db, string name, CancellationToken ct = default)
    {
        var key = NameNormalizer.Normalize(name);
        if (key.Length == 0)
        {
            return null;
        }

        return await db.HorseAliases.Where(a => a.Alias == key).Select(a => a.Horse).FirstOrDefaultAsync(ct);
    }

    /// <summary>Returns the existing rider for <paramref name="name"/> or adds a new one to the context (not saved).</summary>
    public static async Task<Rider> GetOrCreateRiderAsync(HorseShowDbContext db, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        ArgumentException.ThrowIfNullOrEmpty(trimmed);

        var existing = await FindRiderAsync(db, trimmed, ct);
        if (existing is not null)
        {
            return existing;
        }

        // Also check entities added to this context but not yet saved, so one batch does not create duplicates.
        var key = NameNormalizer.Normalize(trimmed);
        var pending = db.ChangeTracker.Entries<Rider>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .FirstOrDefault(r => r.Aliases.Any(a => a.Alias == key));
        if (pending is not null)
        {
            return pending;
        }

        var rider = new Rider { Name = trimmed, Aliases = { new RiderAlias { Alias = key } } };
        db.Riders.Add(rider);
        return rider;
    }

    public static async Task<Horse> GetOrCreateHorseAsync(HorseShowDbContext db, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        ArgumentException.ThrowIfNullOrEmpty(trimmed);

        var existing = await FindHorseAsync(db, trimmed, ct);
        if (existing is not null)
        {
            return existing;
        }

        var key = NameNormalizer.Normalize(trimmed);
        var pending = db.ChangeTracker.Entries<Horse>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .FirstOrDefault(h => h.Aliases.Any(a => a.Alias == key));
        if (pending is not null)
        {
            return pending;
        }

        var horse = new Horse { Name = trimmed, Aliases = { new HorseAlias { Alias = key } } };
        db.Horses.Add(horse);
        return horse;
    }
}
