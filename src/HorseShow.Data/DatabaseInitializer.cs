using HorseShow.Core.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HorseShow.Data;

/// <summary>
/// Startup sequence: ensure directories, back up before any pending migration, migrate, seed.
/// Hosts call <see cref="InitializeAsync"/> before rendering anything that touches the database.
/// </summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<HorseShowDbContext> factory,
    DatabaseOptions options,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);
        Directory.CreateDirectory(options.BackupDirectory);

        await using var db = await factory.CreateDbContextAsync(ct);

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0 && File.Exists(options.DatabasePath))
        {
            var backup = await BackupAsync("pre-migration", ct);
            logger.LogInformation("Backed up database to {Backup} before applying {Count} migration(s)", backup, pending.Count);
        }

        await db.Database.MigrateAsync(ct);
        await SeedAsync(db, ct);
    }

    /// <summary>Consistent snapshot via VACUUM INTO, which is safe under WAL. Returns the backup path.</summary>
    public async Task<string> BackupAsync(string label, CancellationToken ct = default)
    {
        Directory.CreateDirectory(options.BackupDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = Path.Combine(options.BackupDirectory, $"horseshow-{stamp}-{label}.db");

        await using var connection = new SqliteConnection(options.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "VACUUM INTO @path";
        cmd.Parameters.AddWithValue("@path", target);
        await cmd.ExecuteNonQueryAsync(ct);
        return target;
    }

    private static async Task SeedAsync(HorseShowDbContext db, CancellationToken ct)
    {
        if (await db.PointSchedules.AnyAsync(s => s.IsBuiltIn, ct))
        {
            return;
        }

        db.PointSchedules.AddRange(BuiltInSchedules.All());
        await db.SaveChangesAsync(ct);
    }
}
