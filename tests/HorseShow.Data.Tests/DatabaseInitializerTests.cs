using HorseShow.Core.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Data.Tests;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task Migrates_from_empty_and_seeds_built_in_schedules()
    {
        await using var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();

        await using var db = await tmp.Factory.CreateDbContextAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var schedules = await db.PointSchedules.Include(s => s.Rows).Where(s => s.IsBuiltIn).ToListAsync();
        Assert.NotEmpty(schedules);
        Assert.All(schedules, s => Assert.NotEmpty(s.Rows));
    }

    [Fact]
    public async Task Initialize_is_idempotent()
    {
        await using var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();
        await tmp.Initializer.InitializeAsync();

        await using var db = await tmp.Factory.CreateDbContextAsync();
        var builtIns = await db.PointSchedules.CountAsync(s => s.IsBuiltIn);
        Assert.Equal(BuiltInSchedules.All().Count(), builtIns);
    }

    [Fact]
    public async Task Connection_runs_in_wal_mode_with_foreign_keys()
    {
        await using var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();

        await using var db = await tmp.Factory.CreateDbContextAsync();
        await db.Database.OpenConnectionAsync();
        var conn = (SqliteConnection)db.Database.GetDbConnection();

        using var journal = conn.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", (string)journal.ExecuteScalar()!);

        using var fk = conn.CreateCommand();
        fk.CommandText = "PRAGMA foreign_keys;";
        Assert.Equal(1L, (long)fk.ExecuteScalar()!);
    }

    [Fact]
    public async Task Result_requires_rider_or_horse()
    {
        await using var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();

        await using var db = await tmp.Factory.CreateDbContextAsync();
        var season = new Season { Name = "2026", Year = 2026, IsActive = true };
        var show = new Show { Name = "Spring Classic", StartDate = new DateOnly(2026, 4, 12), Season = season };
        var cls = new ShowClass { Name = "Hunter Under Saddle", Show = show };
        db.Results.Add(new Result { ShowClass = cls, Placing = 1 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Result_timestamps_are_stamped_on_save()
    {
        await using var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();

        await using var db = await tmp.Factory.CreateDbContextAsync();
        var season = new Season { Name = "2026", Year = 2026, IsActive = true };
        var show = new Show { Name = "Spring Classic", StartDate = new DateOnly(2026, 4, 12), Season = season };
        var cls = new ShowClass { Name = "Hunter Under Saddle", Show = show };
        var result = new Result { ShowClass = cls, Rider = new Rider { Name = "Jane Smith" }, Placing = 1 };
        db.Results.Add(result);
        await db.SaveChangesAsync();

        Assert.NotEqual(default, result.CreatedAt);
        Assert.Equal(result.CreatedAt, result.UpdatedAt);
    }

    [Fact]
    public async Task Alias_is_unique_across_riders()
    {
        await using var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();

        await using var db = await tmp.Factory.CreateDbContextAsync();
        db.Riders.Add(new Rider { Name = "Jane Smith", Aliases = { new RiderAlias { Alias = "jane smith" } } });
        db.Riders.Add(new Rider { Name = "J. Smith", Aliases = { new RiderAlias { Alias = "jane smith" } } });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Backup_produces_an_openable_copy()
    {
        await using var tmp = new TempDatabase();
        await tmp.Initializer.InitializeAsync();

        await using (var db = await tmp.Factory.CreateDbContextAsync())
        {
            db.Seasons.Add(new Season { Name = "2026", Year = 2026 });
            await db.SaveChangesAsync();
        }

        var path = await tmp.Initializer.BackupAsync("test");
        Assert.True(File.Exists(path));

        var options = new DbContextOptionsBuilder<HorseShowDbContext>().UseSqlite($"Data Source={path}").Options;
        await using var copy = new HorseShowDbContext(options);
        Assert.Equal(1, await copy.Seasons.CountAsync());
        SqliteConnection.ClearAllPools();
    }
}
