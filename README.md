# HorseShow

Windows-native .NET MAUI Blazor Hybrid app for tracking horse show results and
season standings. SQLite + EF Core, MudBlazor UI, paste/CSV/XLSX import.

See [docs/PLAN.md](docs/PLAN.md) for the implementation plan and phases.

## Status

Phase 0 complete: solution skeleton, domain model, EF Core migration, MudBlazor shell,
Web dev host, CI. Target framework is `net10.0` until the .NET 11 GA toolchain ships;
flip `AppTargetFramework` in `Directory.Build.props` and the two version properties in
`Directory.Packages.props` to move.

## Working on it

```bash
# Fast inner loop (any OS): same UI as the desktop app, in a browser, with Hot Reload.
dotnet watch --project src/HorseShow.Web

# Tests
dotnet test tests/HorseShow.Core.Tests
dotnet test tests/HorseShow.Data.Tests

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
| `src/HorseShow.Import` | Paste/CSV/XLSX parsing and name resolution (Phase 3). |
| `src/HorseShow.UI` | MudBlazor pages and components shared by both hosts. |
| `src/HorseShow.App` | MAUI Blazor Hybrid host, Windows only. |
| `src/HorseShow.Web` | Dev-only Blazor Server host for the shared UI. |
| `tests/*` | xUnit. Data tests run against a real SQLite file. |
