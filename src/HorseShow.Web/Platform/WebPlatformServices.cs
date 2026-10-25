using HorseShow.UI.Platform;

namespace HorseShow.Web.Platform;

public sealed class WebAppPaths(string dataDirectory) : IAppPaths
{
    public string DataDirectory { get; } = dataDirectory;
    public string DatabasePath { get; } = Path.Combine(dataDirectory, Data.DatabaseOptions.DefaultFileName);
    public string BackupDirectory { get; } = Path.Combine(dataDirectory, "backups");
}

/// <summary>Browser clipboard needs JS interop and a user gesture; the Import page handles that path itself in Phase 3.</summary>
public sealed class WebClipboardService : IClipboardService
{
    public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
}

/// <summary>The dev host uses InputFile in the Import page instead of a native picker (Phase 3).</summary>
public sealed class WebFilePickerService : IFilePickerService
{
    public Task<PickedFile?> PickAsync(params string[] extensions) => Task.FromResult<PickedFile?>(null);
}

/// <summary>Writes to the dev data folder; the desktop host shows a real Save dialog.</summary>
public sealed class WebFileSaveService(IAppPaths paths) : IFileSaveService
{
    public async Task<string?> SaveAsync(string suggestedFileName, Stream content)
    {
        var exports = Path.Combine(paths.DataDirectory, "exports");
        Directory.CreateDirectory(exports);
        var target = Path.Combine(exports, suggestedFileName);
        await using var file = File.Create(target);
        await content.CopyToAsync(file);
        return target;
    }
}
