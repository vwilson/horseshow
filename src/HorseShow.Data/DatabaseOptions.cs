namespace HorseShow.Data;

/// <summary>Where the SQLite file and its backups live. Supplied by the host.</summary>
public sealed class DatabaseOptions
{
    public const string DefaultFileName = "horseshow.db";

    public required string DatabasePath { get; init; }
    public required string BackupDirectory { get; init; }

    public string ConnectionString => $"Data Source={DatabasePath}";
}
