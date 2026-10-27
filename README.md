# HorseShow

Windows-native .NET MAUI Blazor Hybrid app for tracking horse show results and
season standings. SQLite + EF Core, MudBlazor UI, paste/CSV/XLSX import.

See [docs/PLAN.md](docs/PLAN.md) for the implementation plan and phases.

## Status

All five planned phases are built. Latest: wide-layout import (one column per class),
drag-and-drop files, standings export to CSV/Excel and print, show results export, backup
and restore, database relocation, rider/horse merge, remembered theme and window state,
and a tagged-release workflow. The MAUI host has not yet been built on a Windows machine;
see "Distribution". Target framework is `net10.0` until the .NET 11 GA toolchain ships;
flip `AppTargetFramework` in `Directory.Build.props` and the two version properties in
`Directory.Packages.props` to move.

## Working on it

```bash
# Fast inner loop (any OS): same UI as the desktop app, in a browser, with Hot Reload.
dotnet watch --project src/HorseShow.Web

# Tests
dotnet test tests/HorseShow.Core.Tests
dotnet test tests/HorseShow.Data.Tests
dotnet test tests/HorseShow.UI.Tests
dotnet test tests/HorseShow.Import.Tests

# Windows desktop app (Windows only; needs the MAUI workload once)
dotnet workload install maui-windows
dotnet build src/HorseShow.App
dotnet run --project src/HorseShow.App -f net10.0-windows10.0.19041.0

# Whole solution, including the desktop app (Windows only; on Linux/macOS build the Web host above)
dotnet build HorseShow.slnx

# Add a migration after changing the model
dotnet ef migrations add <Name> --project src/HorseShow.Data --output-dir Migrations
```

The Web host keeps its database in `src/HorseShow.Web/data/` (ignored by git).
The desktop app keeps its database in `%LOCALAPPDATA%\HorseShow\horseshow.db` with backups in
`%LOCALAPPDATA%\HorseShow\backups\`. Theme and window placement go through MAUI `Preferences`,
which an unpackaged app stores in `%LOCALAPPDATA%\Victor Wilson\com.vwilson.horseshow\Settings\`.

## Layout

| Project | Purpose |
|---|---|
| `src/HorseShow.Core` | Domain model, scoring engine, name normalizer. No dependencies. |
| `src/HorseShow.Data` | `HorseShowDbContext`, configurations, migrations, startup initializer. |
| `src/HorseShow.Import` | Paste/CSV/XLSX parsing, column mapping, name resolution, commit and undo. |
| `src/HorseShow.UI` | MudBlazor pages and components shared by both hosts. |
| `src/HorseShow.App` | MAUI Blazor Hybrid host, Windows only. |
| `src/HorseShow.Web` | Dev-only Blazor Server host for the shared UI. |
| `tests/*` | xUnit. Data and UI service tests run against a real SQLite file via `HorseShow.TestSupport`. |

## Supporting a new results format

Most sheets work without changes: the importer detects the delimiter, finds the header row,
recognises class headings, and maps columns by header name or by the shape of the values.
When a real sheet does not come through cleanly:

1. Save it under `tests/HorseShow.Import.Tests/Fixtures/` and add a test that parses it.
2. If a column header is not recognised, add it to `ColumnSynonyms` in
   `src/HorseShow.Import/Mapping/ColumnSynonyms.cs`.
3. If class heading lines are not recognised, extend `ClassHeaderParser` in
   `src/HorseShow.Import/Parsing/ClassHeaderParser.cs`.
4. If a placing or status token is new ("RC", "W/D"), add it to `PlacingParser`.

Once a sheet imports with the right mapping, save the mapping as a profile from the Columns
step; the next paste from the same source maps itself.

## Distribution

`dotnet publish src/HorseShow.App -c Release -r win-x64 --self-contained -o publish` produces an
unpackaged, self-contained folder; `publish\HorseShow.App.exe` runs on any Windows 10 1809+ machine
with no installer, as long as the WebView2 Runtime is present (it ships with Windows 11 and with
Edge on Windows 10). Without `--self-contained` the folder needs the .NET 10 Desktop Runtime
installed; the Windows App SDK is bundled either way. Pushing a tag like `v0.1.0` makes the
`release` job build that folder (with the `v` stripped from the version), zip it and attach it to
a GitHub release.

For an installer or Store distribution, switch to MSIX: set `<WindowsPackageType>MSIX</WindowsPackageType>`
in `src/HorseShow.App/HorseShow.App.csproj`, fill in the `Identity` and `Publisher` in
`Platforms/Windows/Package.appxmanifest`, and sign with a certificate
(`dotnet publish ... -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=true
-p:PackageCertificateThumbprint=...`). For auto-update on top of the unpackaged build,
[Velopack](https://velopack.io) wraps the publish folder and needs no code changes beyond a
startup call; it was left out until a real distribution target exists.

Data lives in `%LOCALAPPDATA%\HorseShow\horseshow.db` unless relocated from Settings.
Backups go in a `backups` folder beside the database.
