using HorseShow.UI.Services;

namespace HorseShow.UI.Tests;

public class MaintenanceTests
{
    [Fact]
    public async Task Backup_restore_round_trip_brings_back_deleted_data()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026 });

        var backup = await f.Maintenance.BackupNowAsync();
        Assert.Single(await f.Maintenance.ListBackupsAsync());
        Assert.True(backup.Bytes > 0);

        await f.Seasons.DeleteAsync(season.Id);
        Assert.Equal((0, 0, 0), await f.Maintenance.CountsAsync());

        await f.Maintenance.RestoreAsync(backup.Path);

        Assert.Equal((1, 0, 0), await f.Maintenance.CountsAsync());
        var backups = await f.Maintenance.ListBackupsAsync();
        Assert.Equal(2, backups.Count); // the manual one plus the automatic pre-restore copy
        Assert.Contains(backups, b => b.Name.Contains("pre-restore"));
    }

    [Fact]
    public async Task Restore_from_an_uploaded_file_brings_back_deleted_data()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var season = await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026 });
        var upload = await File.ReadAllBytesAsync((await f.Maintenance.BackupNowAsync()).Path);
        await f.Seasons.DeleteAsync(season.Id);

        // The upload is staged in a temp file and deleted afterwards, which Windows refuses while anything holds it open.
        await f.Maintenance.RestoreFromStreamAsync(new MemoryStream(upload));

        Assert.Equal((1, 0, 0), await f.Maintenance.CountsAsync());
    }

    [Fact]
    public async Task A_backup_can_be_deleted_right_after_restoring_it()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var backup = await f.Maintenance.BackupNowAsync();
        await f.Maintenance.RestoreAsync(backup.Path);

        await f.Maintenance.DeleteBackupAsync(backup);

        Assert.False(File.Exists(backup.Path));
    }

    [Fact]
    public async Task Restore_rejects_a_file_that_is_not_sqlite()
    {
        await using var f = await ServiceFixture.CreateAsync();
        var bogus = Path.Combine(f.Db.Directory, "bogus.db");
        await File.WriteAllTextAsync(bogus, "hello");

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Maintenance.RestoreAsync(bogus));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Maintenance.RestoreFromStreamAsync(new MemoryStream("nope"u8.ToArray())));
    }

    [Fact]
    public async Task Download_saves_a_copy_through_the_file_service()
    {
        await using var f = await ServiceFixture.CreateAsync();

        var path = await f.Maintenance.DownloadBackupAsync();

        Assert.NotNull(path);
        Assert.StartsWith(f.ExportDirectory, path);
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public async Task Relocating_the_database_copies_it_and_records_the_override()
    {
        await using var f = await ServiceFixture.CreateAsync();
        await f.Seasons.SaveAsync(new SeasonEdit { Name = "2026", Year = 2026 });
        var folder = Path.Combine(f.Db.Directory, "elsewhere");

        Assert.False(f.Maintenance.IsDatabaseRelocated);
        await f.Maintenance.SetDatabaseFolderAsync(folder);

        Assert.True(File.Exists(Path.Combine(folder, "horseshow.db")));
        var reread = new HorseShow.UI.Platform.DefaultAppPaths(f.Db.Directory);
        Assert.Equal(Path.Combine(folder, "horseshow.db"), reread.DatabasePath);
        Assert.Equal(Path.Combine(folder, "backups"), reread.BackupDirectory);

        await f.Maintenance.SetDatabaseFolderAsync(null);
        Assert.Equal(reread.DefaultDatabasePath, new HorseShow.UI.Platform.DefaultAppPaths(f.Db.Directory).DatabasePath);
    }
}
