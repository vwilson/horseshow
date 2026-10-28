# Building and verifying the Windows (MAUI) host

Brief for a Claude Code session running **on a Windows machine** in a local clone of this
repository. Everything else in the repo was developed and tested on Linux, where the MAUI
Windows target cannot finish building (the WinUI XAML compiler and MakePri are Windows-only).
A cloud session already proved the host's C# and XAML compile against MAUI 10.0.120 and fixed
the data path, publish settings and release workflow; **the app has never been launched.**

## Setup

1. `git fetch origin ccr-672e98bb-54859n && git checkout ccr-672e98bb-54859n`. All work and
   the final push go to this branch. Do not open a pull request.
2. .NET 10 SDK (`global.json` pins 10.0.1xx) and the MAUI Windows workload:
   `dotnet workload install maui-windows`. WebView2 Runtime must be present (it is on any
   up-to-date Windows 10/11). Central package management lives in `Directory.Packages.props`;
   the target framework comes from `AppTargetFramework` in `Directory.Build.props`.
3. Sanity check first: `dotnet build src/HorseShow.Web` and `dotnet test` on each of
   `tests/HorseShow.Core.Tests`, `tests/HorseShow.Data.Tests`, `tests/HorseShow.UI.Tests`,
   `tests/HorseShow.Import.Tests` (160 tests). A Windows-only failure here (path separators,
   CRLF in `tests/HorseShow.Import.Tests/Fixtures/`, temp paths) is a finding to fix and report.

## The host project, `src/HorseShow.App/`

Hand-written from the stock .NET 10 MAUI Blazor template; expect template-level slips, not
design problems. Most likely trouble spots, in order:

- `Platform/MauiPlatformServices.cs`: `MauiFileSaveService` uses WinRT `FileSavePicker` under
  `#if WINDOWS`, gets the window via
  `Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window`,
  then `WinRT.Interop.WindowNative.GetWindowHandle` and `InitializeWithWindow.Initialize`,
  and writes through `StorageFile.OpenStreamForWriteAsync`. `MauiUserSettings` wraps
  `Preferences.Default`. `MauiFilePickerService` uses `FilePicker.Default` with a WinUI
  file-type map.
- `App.xaml.cs`: `CreateWindow` restores and saves window size/position through `Preferences`
  and the `Window.Destroying` event.
- `MauiProgram.cs`: data folder is `%LOCALAPPDATA%\HorseShow`; database initialization runs on a
  background task that opens a `StartupGate` the UI waits on (progress page, then the shell, or
  an error page with the database path).
- `HorseShow.App.csproj` (`WindowsPackageType=None`, unpackaged), `Platforms/Windows/Package.appxmanifest`,
  `Platforms/Windows/app.manifest`, `MainPage.xaml` (`BlazorWebView`, root component
  `HorseShow.UI.Routes` at `#app`), `wwwroot/index.html`.

Shared projects (`src/HorseShow.UI`, `Data`, `Core`, `Import`) should not need changes. If one is
genuinely required for the host, keep it minimal and re-run the tests.

## Steps

1. `dotnet build src/HorseShow.App -f net10.0-windows10.0.19041.0`. Fix until clean.
2. Run it: `dotnet run --project src/HorseShow.App -f net10.0-windows10.0.19041.0`, or F5 in
   Visual Studio with the "Windows Machine" profile. Confirm the window opens at 1400x900 with
   the MudBlazor shell and a "No active season yet" dashboard. First run creates
   `%LOCALAPPDATA%\HorseShow\horseshow.db` and seeds the built-in point schedules.
3. Exercise the native-only paths, which have no coverage from Linux:
   - Seasons → New season (pick the Classic schedule) → Save. Shows → New show → Add class →
     quick-add two results (rider and horse autocompletes offer "Create").
   - Import results → File tab → "Choose a file" must open a native picker. Use
     `tests/HorseShow.Import.Tests/Fixtures/wide-layout.csv`, then Columns → Names → Preview →
     Complete. Also try dragging a file onto the drop zone.
   - Import results → Paste tab → "Paste from clipboard" with a table copied from Excel or
     `tests/HorseShow.Import.Tests/Fixtures/dash-listing.txt`.
   - Standings → ⋮ menu → Export CSV and Export Excel must open the Windows save dialog and write
     the file. Print must open the print dialog.
   - Riders → select two → "Set group…" and "Merge selected".
   - Settings → Back up now; Save a copy… (save dialog); Restore; toggle dark mode; close and
     reopen the app and confirm dark mode and the window size/position are remembered.
4. `dotnet publish src/HorseShow.App -c Release -r win-x64 --self-contained -o publish` and run
   `publish\HorseShow.App.exe` from that folder.
5. Commit on `ccr-672e98bb-54859n` with a message listing what changed and why, then
   `git push -u origin ccr-672e98bb-54859n`.

## Done means

- `dotnet build HorseShow.slnx` succeeds on Windows with zero errors.
- The app launches, creates its database, and every item in step 3 works or is documented
  with the exact error.
- The publish folder runs standalone.
- `README.md` corrected if any command there was wrong on real Windows.
- The final message lists each file changed with one sentence per change, and anything still broken.
