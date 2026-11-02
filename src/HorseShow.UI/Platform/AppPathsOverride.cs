using System.Text.Json;

namespace HorseShow.UI.Platform;

/// <summary>
/// Lets the user keep the database somewhere else (OneDrive, a USB stick). The override lives in a
/// tiny JSON file in the default data folder, read by the host before the database is opened.
/// </summary>
public static class AppPathsOverride
{
    private const string FileName = "paths.json";

    private sealed class Model
    {
        public string? DatabasePath { get; set; }
    }

    public static string? ReadDatabasePath(string dataDirectory)
    {
        try
        {
            var file = Path.Combine(dataDirectory, FileName);
            if (!File.Exists(file))
            {
                return null;
            }

            var model = JsonSerializer.Deserialize<Model>(File.ReadAllText(file));
            return string.IsNullOrWhiteSpace(model?.DatabasePath) ? null : model.DatabasePath;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void WriteDatabasePath(string dataDirectory, string? databasePath)
    {
        Directory.CreateDirectory(dataDirectory);
        var file = Path.Combine(dataDirectory, FileName);
        if (databasePath is null)
        {
            File.Delete(file);
            return;
        }

        File.WriteAllText(file, JsonSerializer.Serialize(new Model { DatabasePath = databasePath }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>Default implementation of <see cref="IAppPaths"/> for hosts: a base folder plus the optional override.</summary>
public sealed class DefaultAppPaths : IAppPaths
{
    public DefaultAppPaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        DefaultDatabasePath = Path.Combine(dataDirectory, "horseshow.db");
        DatabasePath = AppPathsOverride.ReadDatabasePath(dataDirectory) ?? DefaultDatabasePath;
        BackupDirectory = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "backups");
    }

    public string DataDirectory { get; }
    public string DefaultDatabasePath { get; }
    public string DatabasePath { get; }
    public string BackupDirectory { get; }
}
