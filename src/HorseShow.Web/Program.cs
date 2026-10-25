using HorseShow.Data;
using HorseShow.UI;
using HorseShow.UI.Platform;
using HorseShow.Web.Components;
using HorseShow.Web.Platform;

// Dev-only host. Runs the same UI as the MAUI app in a browser with Hot Reload.
// Database lives in ./data under the project so it never collides with the desktop app's.

var builder = WebApplication.CreateBuilder(args);

var paths = new WebAppPaths(Path.Combine(builder.Environment.ContentRootPath, "data"));
builder.Services.AddSingleton<IAppPaths>(paths);
builder.Services.AddSingleton<IClipboardService, WebClipboardService>();
builder.Services.AddSingleton<IFilePickerService, WebFilePickerService>();
builder.Services.AddSingleton<IFileSaveService, WebFileSaveService>();

builder.Services.AddHorseShowData(new DatabaseOptions
{
    DatabasePath = paths.DatabasePath,
    BackupDirectory = paths.BackupDirectory,
});
builder.Services.AddHorseShowUi();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

var gate = app.Services.GetRequiredService<HorseShow.UI.Services.StartupGate>();
try
{
    await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    gate.MarkReady();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database initialization failed");
    gate.MarkFailed(ex);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(HorseShow.UI.Routes).Assembly);

app.Run();
