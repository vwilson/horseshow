using HorseShow.Data;
using HorseShow.UI.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

public sealed record BackupFile(string Path, string Name, DateTime Modified, long Bytes);

/// <summary>Backups, restore, and the database location. Everything here touches the file the app is running on.</summary>
public sealed class MaintenanceViewService(
    DatabaseInitializer initializer,
    DatabaseOptions options,
    IAppPaths paths,
    IDbContextFactory<HorseShowDbContext> factory,
    IFileSaveService fileSave)
{
    private const string SqliteMagic = "SQLite format 3\0";

    public async Task<BackupFile> BackupNowAsync(CancellationToken ct = default)
    {
        var path = await initializer.BackupAsync("manual", ct);
        return Describe(new FileInfo(path));
    }

    public Task<List<BackupFile>> ListBackupsAsync()
    {
        var dir = new DirectoryInfo(options.BackupDirectory);
        var files = dir.Exists
            ? dir.GetFiles("*.db").OrderByDescending(f => f.LastWriteTimeUtc).Select(Describe).ToList()
            : [];
        return Task.FromResult(files);
    }

    public Task DeleteBackupAsync(BackupFile file)
    {
        if (Path.GetFullPath(Path.GetDirectoryName(file.Path)!) == Path.GetFullPath(options.BackupDirectory))
        {
            File.Delete(file.Path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Takes a fresh backup and hands it to the host's save dialog (or the Web host's exports folder).</summary>
    public async Task<string?> DownloadBackupAsync(CancellationToken ct = default)
    {
        var backup = await BackupNowAsync(ct);
        await using var stream = File.OpenRead(backup.Path);
        return await fileSave.SaveAsync($"horseshow-backup-{DateTime.Now:yyyyMMdd-HHmm}.db", stream);
    }

    /// <summary>
    /// Replaces the live database with <paramref name="sourcePath"/>. The current file is backed up first
    /// as "pre-restore". Callers must reload the UI afterwards; every cached query is stale.
    /// </summary>
    public async Task RestoreAsync(string sourcePath, CancellationToken ct = default)
    {
        if (!IsSqliteFile(sourcePath))
        {
            throw new InvalidOperationException("That file is not a SQLite database.");
        }

        if (File.Exists(options.DatabasePath))
        {
            await initializer.BackupAsync("pre-restore", ct);
        }

        // SQLite's online backup API copies pages into the live database under its own locks,
        // so nothing has to be closed, moved, or deleted. Pooled connections see the new content.
        await using (var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly"))
        await using (var target = new SqliteConnection(options.ConnectionString))
        {
            await source.OpenAsync(ct);
            await target.OpenAsync(ct);
            source.BackupDatabase(target);
        }

        // The restored file may predate the current schema.
        await initializer.InitializeAsync(ct);
    }

    public async Task RestoreFromStreamAsync(Stream content, CancellationToken ct = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"horseshow-restore-{Guid.NewGuid():N}.db");
        try
        {
            await using (var file = File.Create(temp))
            {
                await content.CopyToAsync(file, ct);
            }

            await RestoreAsync(temp, ct);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public string DefaultDatabasePath => (paths as DefaultAppPaths)?.DefaultDatabasePath ?? options.DatabasePath;

    public bool IsDatabaseRelocated => !string.Equals(Path.GetFullPath(options.DatabasePath), Path.GetFullPath(DefaultDatabasePath), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Points the app at a database in <paramref name="folder"/>. Copies the current file there when none
    /// exists. Takes effect on the next launch, which is why the page asks for a restart.
    /// </summary>
    public async Task SetDatabaseFolderAsync(string? folder, CancellationToken ct = default)
    {
        if (folder is null)
        {
            AppPathsOverride.WriteDatabasePath(paths.DataDirectory, null);
            return;
        }

        var target = Path.Combine(Path.GetFullPath(folder), DatabaseOptions.DefaultFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (!File.Exists(target) && File.Exists(options.DatabasePath))
        {
            // VACUUM INTO gives a consistent copy even with WAL pages outstanding.
            await using var connection = new SqliteConnection(options.ConnectionString);
            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "VACUUM INTO @path";
            cmd.Parameters.AddWithValue("@path", target);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        AppPathsOverride.WriteDatabasePath(paths.DataDirectory, target);
    }

    public async Task<(int Seasons, int Shows, int Results)> CountsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.Seasons.CountAsync(ct), await db.Shows.CountAsync(ct), await db.Results.CountAsync(ct));
    }

    private static BackupFile Describe(FileInfo f) => new(f.FullName, f.Name, f.LastWriteTime, f.Length);

    private static bool IsSqliteFile(string path)
    {
        try
        {
            using var s = File.OpenRead(path);
            var buffer = new byte[16];
            return s.Read(buffer, 0, 16) == 16 && System.Text.Encoding.ASCII.GetString(buffer) == SqliteMagic;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
