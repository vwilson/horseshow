# HorseShow

Windows-native .NET MAUI Blazor Hybrid app for tracking horse show results and
season standings. SQLite + EF Core, MudBlazor UI, paste/CSV/XLSX import.

See [docs/PLAN.md](docs/PLAN.md) for the implementation plan and phases.

## Status

Phase 3 complete: paste, CSV and XLSX import through a four-step wizard (source, columns,
names, preview) with auto-mapping, saved mapping profiles, fuzzy name matching, duplicate
handling, and undo from import history. Built against a fixture corpus of plausible sheet
shapes; see "Supporting a new results format" below. Next: Phase 4 (XLSX ergonomics, wide
layouts) and Phase 5 (export, backup, packaging). Target framework is `net10.0` until the .NET 11 GA toolchain ships;
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

# Add a migration after changing the model
dotnet ef migrations add <Name> --project src/HorseShow.Data --output-dir Migrations
```

The Web host keeps its database in `src/HorseShow.Web/data/` (ignored by git).
The desktop app keeps its database under `FileSystem.AppDataDirectory\HorseShow\`,
which is `%LOCALAPPDATA%\HorseShow\` when run unpackaged.

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
