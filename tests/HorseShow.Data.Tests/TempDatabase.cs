using HorseShow.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HorseShow.Data.Tests;

/// <summary>
/// A real SQLite file in a temp directory, wired exactly as the hosts wire it.
/// The in-memory provider is not used anywhere: it would not exercise the migration,
/// the check constraint, or the pragma interceptor.
/// </summary>
public sealed class TempDatabase : IAsyncDisposable
{
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "horseshow-tests", Guid.NewGuid().ToString("N"));
    public DatabaseOptions Options { get; }
    public ServiceProvider Services { get; }
    public IDbContextFactory<HorseShowDbContext> Factory => Services.GetRequiredService<IDbContextFactory<HorseShowDbContext>>();
    public DatabaseInitializer Initializer => Services.GetRequiredService<DatabaseInitializer>();

    public TempDatabase()
    {
        System.IO.Directory.CreateDirectory(Directory);
        Options = new DatabaseOptions
        {
            DatabasePath = Path.Combine(Directory, DatabaseOptions.DefaultFileName),
            BackupDirectory = Path.Combine(Directory, "backups"),
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHorseShowData(Options);
        Services = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch (IOException) { }
    }
}
