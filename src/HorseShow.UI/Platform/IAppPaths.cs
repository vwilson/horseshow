namespace HorseShow.UI.Platform;

/// <summary>Host-provided file locations.</summary>
public interface IAppPaths
{
    string DataDirectory { get; }
    string DatabasePath { get; }
    string BackupDirectory { get; }
}
