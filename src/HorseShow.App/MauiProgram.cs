using HorseShow.App.Platform;
using HorseShow.Data;
using HorseShow.UI;
using HorseShow.UI.Platform;
using HorseShow.UI.Services;
using Microsoft.Extensions.Logging;

namespace HorseShow.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var paths = new MauiAppPaths();
        builder.Services.AddSingleton<IAppPaths>(paths);
        builder.Services.AddSingleton<IClipboardService, MauiClipboardService>();
        builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();
        builder.Services.AddSingleton<IFileSaveService, MauiFileSaveService>();

        builder.Services.AddHorseShowData(new DatabaseOptions
        {
            DatabasePath = paths.DatabasePath,
            BackupDirectory = paths.BackupDirectory,
        });
        builder.Services.AddHorseShowUi();

        var app = builder.Build();

        // Migrate and seed off the UI thread; Routes shows progress until the gate opens.
        var gate = app.Services.GetRequiredService<StartupGate>();
        var initializer = app.Services.GetRequiredService<DatabaseInitializer>();
        _ = Task.Run(async () =>
        {
            try
            {
                await initializer.InitializeAsync();
                gate.MarkReady();
            }
            catch (Exception ex)
            {
                gate.MarkFailed(ex);
            }
        });

        return app;
    }
}
