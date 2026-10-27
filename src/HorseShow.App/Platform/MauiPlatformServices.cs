using HorseShow.Data;
using HorseShow.UI.Platform;

namespace HorseShow.App.Platform;

/// <summary>MAUI Preferences-backed settings. Survives unpackaged and packaged installs alike.</summary>
public sealed class MauiUserSettings : IUserSettings
{
    public Task<string?> GetAsync(string key)
        => Task.FromResult(Preferences.Default.ContainsKey(key) ? Preferences.Default.Get<string?>(key, null) : null);

    public Task SetAsync(string key, string? value)
    {
        if (value is null) Preferences.Default.Remove(key); else Preferences.Default.Set(key, value);
        return Task.CompletedTask;
    }
}

public sealed class MauiClipboardService : IClipboardService
{
    public async Task<string?> GetTextAsync()
        => Clipboard.Default.HasText ? await Clipboard.Default.GetTextAsync() : null;
}

public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<PickedFile?> PickAsync(params string[] extensions)
    {
        var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = extensions,
        });

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose a results file",
            FileTypes = fileTypes,
        });

        return result is null ? null : new PickedFile(result.FileName, result.OpenReadAsync);
    }
}

public sealed class MauiFileSaveService : IFileSaveService
{
    public async Task<string?> SaveAsync(string suggestedFileName, Stream content)
    {
#if WINDOWS
        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
        };
        var ext = Path.GetExtension(suggestedFileName);
        picker.FileTypeChoices.Add(ext.TrimStart('.').ToUpperInvariant(), [ext]);

        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("No window available for the save dialog.");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        await using var target = await file.OpenStreamForWriteAsync();
        target.SetLength(0);
        await content.CopyToAsync(target);
        return file.Path;
#else
        throw new PlatformNotSupportedException();
#endif
    }
}
