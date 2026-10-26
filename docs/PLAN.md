# HorseShow Season Tracker: Implementation Plan

A Windows-native desktop app for recording horse show results across a season,
computing season standings, and importing results by pasting a table or loading
CSV/XLSX files.

| Decision | Choice |
|---|---|
| Runtime | .NET 11 (`net11.0-windows10.0.19041.0`) |
| Shell | .NET MAUI Blazor Hybrid, Windows target only to start |
| UI | Blazor + MudBlazor, in a shared Razor Class Library |
| Storage | SQLite via EF Core (`Microsoft.EntityFrameworkCore.Sqlite`), code-first migrations |
| Import | Clipboard/paste (TSV, CSV, whitespace), CSV files, XLSX files |
| Tests | xUnit; bUnit for components where worthwhile |

> **Timing note.** As of late October 2026, .NET 11 is at release-candidate stage with GA
> expected in November. Expect the MAUI workload, EF Core 11, and MudBlazor builds for
> `net11.0` to be prerelease until then. Everything below works identically on `net10.0`
> if a stable toolchain is needed first; the TFM is a one-line change.

---

## 1. Goals and non-goals

**Goals**

- Record shows, classes, and placings/scores for riders and horses over a season.
- Compute season standings (by rider, by horse, or by rider+horse combination)
  from configurable point schedules.
- Get results in fast: paste from a website or Excel, or import CSV/XLSX, with
  column mapping and name matching so the user is not retyping anything.
- Run as a native Windows desktop app with a local database and no server.

**Non-goals (v1)**

- Multi-user sync or cloud storage. Single local SQLite file, with backup/restore.
- Mobile targets. The RCL keeps the door open; nothing in v1 depends on it.
- Entering results *live* at a show (ring-side timing, judge cards, etc.).

---

## 2. Architecture

### 2.1 Why MAUI Blazor Hybrid (and the fallback)

MAUI Blazor Hybrid is the canonical "Blazor, native on Windows" path: a WinUI 3 window
hosting `BlazorWebView`, with MAUI essentials (FilePicker, Clipboard, FileSystem
paths) available directly. Target only Windows for now to avoid dragging in the
Android/iOS workloads.

**Fallback:** if the MAUI workload for .NET 11 causes friction, the same RCL can be
hosted in a WPF window via `Microsoft.AspNetCore.Components.WebView.Wpf`. That gives
a plain `.exe`, no MSIX, no Windows App SDK runtime, and simpler packaging. The only
code that changes is the host project and the platform service implementations
(file picker, clipboard). The plan keeps those behind interfaces for exactly this reason.

### 2.2 Solution layout

```
HorseShow.sln
├── src/
│   ├── HorseShow.Core/          # Domain model, scoring engine. No EF, no UI. net11.0
│   ├── HorseShow.Data/          # DbContext, configurations, migrations, repositories. net11.0
│   ├── HorseShow.Import/        # Parsers (paste/CSV/XLSX), column mapping, name resolution. net11.0
│   ├── HorseShow.UI/            # Razor Class Library: MudBlazor pages, components, view services. net11.0
│   ├── HorseShow.App/           # MAUI Blazor Hybrid host (Windows). net11.0-windows10.0.19041.0
│   └── HorseShow.Web/           # Optional Blazor Server host for the same RCL. Dev-only. net11.0
├── tests/
│   ├── HorseShow.Core.Tests/
│   ├── HorseShow.Import.Tests/  # Fixture files: sample pastes, CSVs, XLSX
│   ├── HorseShow.Data.Tests/    # Real SQLite in a temp file, not the in-memory provider
│   └── HorseShow.UI.Tests/      # bUnit, selective
├── docs/
└── .github/workflows/build.yml  # windows-latest: restore, build, test
```

**Why `HorseShow.Web` exists:** MAUI Windows inner-loop (build, deploy, launch) is
slow and Hot Reload is flaky in a `BlazorWebView`. Hosting the identical RCL in a
Blazor Server project gives full Hot Reload for UI work. The Web host uses the same
SQLite file in a dev folder. This is the pattern from the official
"Blazor Hybrid and Web App" template; it costs one small project.

### 2.3 Dependency direction

```
App / Web  →  UI  →  Import  →  Data  →  Core
                 ↘─────────────────────↗
```

`Core` has zero package references. `Data` references `Core` and EF Core. `Import`
references `Data` (for lookups during name resolution) and the parsing libraries.
`UI` references everything below it and MudBlazor. Hosts reference `UI` and provide
platform services.

### 2.4 Platform abstractions (in `UI`, implemented in hosts)

```csharp
public interface IFilePickerService   { Task<PickedFile?> PickAsync(params string[] extensions); }
public interface IFileSaveService     { Task<string?> SaveAsync(string suggestedName, Stream content); }
public interface IClipboardService    { Task<string?> GetTextAsync(); }
public interface IAppPaths            { string DataDirectory { get; } string BackupDirectory { get; } }
```

MAUI implements these with `FilePicker`, `FileSaver` (CommunityToolkit.Maui), `Clipboard`,
and `FileSystem.AppDataDirectory`. The Web host implements them with `InputFile`,
a download endpoint, and a dev folder.

---

## 3. Domain model

Horse show scoring varies by organization (hunter/jumper, western, 4-H, IEA, dressage),
so the model separates **what happened** (placings, raw scores) from **what it is worth**
(points), and keeps the points rules configurable per season or division.

### 3.1 Entities

```
Season            Id, Name, Year, Organization, IsActive, StandingsBasis (Rider | Horse | Combination),
                  TopNShowsCounted?, MinShowsForAward?, DefaultPointScheduleId?

Show              Id, SeasonId, Name, StartDate, EndDate?, Venue?, Notes?

Division          Id, SeasonId, Name, SortOrder, PointScheduleId?  (overrides season default)
                  e.g. "Walk-Trot 11 & Under", "Open Hunter", "Green Horse"

ShowClass         Id, ShowId, DivisionId?, ClassNumber?, Name, EntryCount?, ClassType (Placed | Scored | Both),
                  JudgeName?, Notes?

Rider             Id, Name, DisplayName?, Barn?, Notes?, IsArchived
Horse             Id, Name, Owner?, RegistrationNumber?, Notes?, IsArchived
RiderAlias        Id, RiderId, Alias          (normalized; used by import matching)
HorseAlias        Id, HorseId, Alias

Result            Id, ShowClassId, RiderId?, HorseId?, BackNumber?,
                  Placing?, PlacingStatus (Placed | NoPlace | Scratched | DQ | Eliminated),
                  Score?, ScoreNote?, Points?, PointsSource (Computed | Imported | Manual),
                  ImportBatchId?, CreatedAt, UpdatedAt
                  Constraint: RiderId or HorseId must be non-null.

PointSchedule     Id, SeasonId?, Name, Mode (Fixed | ByEntryCount), IsBuiltIn
PointScheduleRow  Id, PointScheduleId, Placing, Points, MinEntries (0 for Fixed mode)

ImportBatch       Id, SeasonId, ShowId?, Source (Paste | Csv | Xlsx), FileName?, ImportedAt,
                  RowCount, RawContentHash, MappingProfileId?, Notes
                  (Result.ImportBatchId lets the user undo an entire import.)

MappingProfile    Id, Name, SourceHint, ColumnMapJson, ParserOptionsJson
                  (saved "this is how the XYZ show software exports results" templates)
```

### 3.2 Modelling choices worth calling out

- **Rider and Horse both optional on Result, at least one required.** Halter classes
  have no rider; equitation standings often ignore the horse; many 4-H and open circuits
  track the combination. `Season.StandingsBasis` picks the aggregation key.
- **Placing vs Score vs Points are three different columns.** Hunter classes produce a
  judge's score and a placing; western classes produce only a placing; some orgs publish
  points directly. The import maps whichever columns exist; the scoring engine fills
  `Points` only when `PointsSource == Computed`.
- **`EntryCount` lives on the class, not derived from rows.** Many point schedules scale
  by number of entries, and imported results often list only the top 6 or 8 placings.
  When absent, the engine falls back to the number of result rows and flags it.
- **Aliases instead of fuzzy matching at query time.** Fuzzy matching happens once, in the
  import wizard, and the user's decision is recorded as an alias. Later imports with the
  same spelling resolve instantly and deterministically.
- **Points are stored, not computed on read.** Standings must be reproducible even after
  a schedule is edited mid-season. A "Recalculate points" command re-runs the engine for a
  season or show and shows a diff before applying.

### 3.3 Scoring engine (`HorseShow.Core`)

Pure, synchronous, fully unit-tested. No EF types.

```csharp
public static class PointsCalculator
{
    public static decimal? PointsFor(PointSchedule schedule, int placing, int entryCount);
}

public sealed class StandingsCalculator
{
    public IReadOnlyList<StandingLine> Compute(Season season, IEnumerable<Result> results,
                                               StandingsOptions options);
}
```

`StandingsOptions` covers: basis (rider/horse/combination), division filter, count
only top-N shows, require minimum shows, tie-break ordering (total points, then number
of firsts, then number of shows, then name). Output is a flat list of lines with
per-show breakdown for drill-down.

Seed the built-in schedules so the app is useful on first launch, e.g. a classic
`10-6-4-2-1-0.5` fixed schedule and a by-entry-count schedule; the user can clone and edit.

---

## 4. Data layer (`HorseShow.Data`)

- `HorseShowDbContext` with Fluent configurations in `Configurations/` (one class per entity).
- Registered via `AddDbContextFactory<HorseShowDbContext>()`. Blazor components resolve
  `IDbContextFactory` and create a short-lived context per operation. Never inject a
  scoped `DbContext` into components in Blazor Hybrid; the scope lives as long as the window.
- SQLite settings: `journal_mode=WAL`, `foreign_keys=ON`, busy timeout. Set via a
  connection interceptor or `PRAGMA` on open.
- Database file: `{AppDataDirectory}\horseshow.db`. Path is overridable in Settings
  (lets the user keep it in OneDrive or on a USB stick).
- Migrations are applied at startup with `Database.Migrate()`, behind a splash state so
  the UI never renders against an unmigrated schema. Back up the file automatically before
  applying any pending migration.
- Indexes: `Rider.Name`, `Horse.Name`, alias tables (unique on normalized alias),
  `Result(ShowClassId)`, `Result(RiderId)`, `Result(HorseId)`, `Result(ImportBatchId)`.
- Name normalization is a single `Core` function (trim, collapse whitespace, casefold,
  strip punctuation, "Last, First" → "First Last") used by both the alias unique index
  population and the import matcher.
- Backup = `VACUUM INTO 'path'` (consistent snapshot even under WAL). Restore = close
  factory, copy file, re-open, run `Migrate()`.
- Repositories are thin and query-shaped (`IStandingsQuery`, `IShowRepository`), not generic.

---

## 5. Import pipeline (`HorseShow.Import`)

This is the feature that justifies the app, so it gets the most design attention.

### 5.1 Pipeline stages

```
Acquire → Tokenize → Detect structure → Map columns → Normalize cells
        → Resolve names → Validate → Preview → Commit (one ImportBatch, one transaction)
```

Each stage is a plain class with an input/output record so it can be unit-tested with
fixture files and so the wizard UI can show the intermediate result at every step.

### 5.2 Acquire

| Source | Mechanism | Notes |
|---|---|---|
| Paste | `MudTextField` multiline + "Paste from clipboard" button via `IClipboardService` | Excel and most web tables paste as TSV. Handles plain text too. |
| CSV | `IFilePickerService` → stream | CsvHelper (or `Sep` for speed; CsvHelper is fine at this scale) |
| XLSX | `IFilePickerService` → stream | ClosedXML (MIT, read+write, also used for export). Sheet picker if multiple sheets. |
| Drag-and-drop | Later | `BlazorWebView` drop needs JS interop; defer to a polish phase. |

### 5.3 Tokenize and detect structure

Delimiter detection for pasted text, in order: tab, comma, pipe, semicolon, then
runs of two or more spaces (fixed-width-ish output from PDFs and old show software).
Score each candidate by column-count consistency across lines and pick the best.

Row classification, since real pastes are messy:

- **Header row**: first row whose cells match known column synonyms.
- **Section/class row**: a row with one non-empty cell that looks like
  `Class 12 - Hunter Under Saddle` or `#12 Hunter Under Saddle (8 entries)`.
  Supports multi-class pastes: each section row starts a new class and subsequent data
  rows belong to it.
- **Data row**: anything else with a placing-like or back-number-like first cell.
- **Noise**: blank lines, page footers, "Judge: ...", repeated headers.

Supported layouts in v1: one class per paste, and multi-class pastes with section rows.
Wide layouts (one row per entry, one column per class) are a v2 item.

### 5.4 Column mapping

Target fields: `ClassNumber`, `ClassName`, `Placing`, `BackNumber`, `Rider`, `Horse`,
`Owner`, `Score`, `Points`, `EntryCount`, `Ignore`.

Auto-map by synonym table (`Place|Placing|Pl|Pos|Ribbon` → `Placing`,
`Rider|Exhibitor|Handler|Competitor` → `Rider`, `Back #|Number|No.|Entry|Entry #` → `BackNumber`,
and so on). The user confirms or corrects in a mapping grid (one dropdown per detected
column, with a live sample of the first few values). "Save as profile" stores the result
as a `MappingProfile` so the next paste from the same source maps itself.

A single "Rider / Horse" combined column (`Jane Smith on Blue Moon`) is split by a
small set of regexes (`on`, `/`, `riding`, parentheses) with a fallback to manual.

### 5.5 Normalize cells

- Placing: `1`, `1st`, `1.`, `First`, `R` (reserve → 2nd, configurable), and status
  tokens `DQ`, `SCR`, `NP`, `EL`, `WD` → `PlacingStatus`.
- Score: decimal, strips `%` and stray text; dressage-style `65.432%` kept as decimal.
- Names: the shared `Core` normalizer. Keep the original text for display.
- Class header: extracts class number, name, and `(N entries)` when present.

### 5.6 Resolve names

For every distinct rider/horse string in the batch:

1. Exact match on normalized alias → auto-resolve, no prompt.
2. Fuzzy match (Jaro-Winkler ≥ 0.92, also token-sorted comparison to catch
   `Smith, Jane` vs `Jane Smith`) → suggest, user confirms.
3. No match → "Create new" (default) or "Pick existing".

The resolution grid shows each unknown name once, with the suggested match, a confidence
badge, and a dropdown. Confirming writes an alias, so the question never comes back.
"Apply to all similar" handles the common case of a dozen rows for the same horse.

### 5.7 Validate and preview

Rules produce warnings (import proceeds) or errors (row excluded until fixed):

- Error: no class resolved for a data row; neither rider nor horse resolved.
- Warning: duplicate placing within a class; placing exceeds entry count; result already
  exists for this rider/horse/class (offer replace or skip); score present but class type
  is Placed-only.

The preview is a `MudDataGrid` grouped by class, with inline editing for the odd
hand-fix, and a summary chip row (`42 rows · 3 classes · 2 new riders · 1 warning`).

### 5.8 Commit

Single transaction: create or match the `Show` and `ShowClass` rows, insert `Result`
rows tagged with a new `ImportBatch`, compute points, write aliases. The Import History
page lists batches with an **Undo** that deletes the batch's results (and the classes it
created if they are now empty), guarded by a confirmation dialog.

---

## 6. UI (`HorseShow.UI`, MudBlazor)

Layout: `MudLayout` with a `MudAppBar` (active season selector, global search) and a
`MudNavMenu` drawer. Dark/light theme toggle persisted in settings.

| Route | Purpose | Key components |
|---|---|---|
| `/` | Dashboard: active season, top standings cards, recent shows, "Import results" CTA | `MudCard`, `MudTable` |
| `/seasons` | List, create, set active, clone settings from a prior season | `MudDataGrid`, dialog |
| `/seasons/{id}` | Season settings: standings basis, point schedule, divisions | `MudTabs` |
| `/shows` | Shows in active season | `MudDataGrid`, date filters |
| `/shows/{id}` | Classes in the show; per-class result grid with inline edit, add row, reorder | `MudExpansionPanels` + `MudDataGrid` |
| `/import` | Wizard: Source → Parse → Map → Resolve → Preview → Done | `MudStepper` |
| `/import/history` | Batches, undo | `MudTable` |
| `/standings` | Standings table with basis/division filters, drill-down per line, export | `MudDataGrid`, `MudChart` sparkline (later) |
| `/riders`, `/horses` | CRUD, archive, aliases, merge duplicates | `MudDataGrid`, merge dialog |
| `/schedules` | Point schedule editor, clone built-ins | editable `MudTable` |
| `/settings` | DB path, backup/restore, theme, about | forms |

Conventions:

- Pages are thin; each delegates to a `*ViewService` in `UI/Services` that talks to the
  data layer. Keeps components testable with bUnit and keeps logic out of `.razor`.
- All long operations (import commit, recalculation, backup) run through a small
  `IBusyService` that drives a global `MudOverlay` + progress, and are cancellable
  where it makes sense.
- `MudSnackbar` for results of commands; `MudDialog` for confirmations.
- Keyboard-first result entry on the show page: Tab moves across placing → back number
  → rider → horse, Enter adds a row. This is the manual-entry path when there is nothing
  to paste.

---

## 7. MAUI host (`HorseShow.App`)

- `MauiProgram.cs`: `AddMauiBlazorWebView()`, `AddMudServices()`, `AddDbContextFactory`,
  register platform services, `AddHorseShowUi()` extension from the RCL.
- `wwwroot/index.html` includes MudBlazor CSS/JS and the RCL static web assets.
- Windows-only TFM in the csproj; drop the Android/iOS/MacCatalyst TFMs from the template.
- `WindowsPackageType=None` for dev builds so F5 launches an unpackaged exe without
  developer-mode MSIX sideloading. Add MSIX packaging only in the distribution phase.
- Startup sequence: create data dir → back up if migration pending → `Migrate()` → seed
  built-in schedules if missing → render. Failures show a MudBlazor error page with the
  DB path and a "Restore from backup" button rather than a dead window.
- Window: remember size/position in settings; minimum size 1100×700 for the grids.

---

## 8. Packages

| Package | Purpose |
|---|---|
| `Microsoft.Maui.Controls`, `Microsoft.AspNetCore.Components.WebView.Maui` | Host |
| `CommunityToolkit.Maui` | `FileSaver` for exports |
| `MudBlazor` | UI |
| `Microsoft.EntityFrameworkCore.Sqlite`, `.Design` | Data |
| `CsvHelper` | CSV read/write |
| `ClosedXML` | XLSX read/write |
| `FuzzySharp` or a small vendored Jaro-Winkler | Name matching (one file; vendoring avoids a dependency) |
| `xunit`, `bunit`, `FluentAssertions` (or built-in `Assert`) | Tests |

Pin versions once the .NET 11 GA packages land; use `Directory.Packages.props` with
central package management from day one.

---

## 9. Testing strategy

- **Core**: table-driven tests for `PointsCalculator` (every schedule mode, out-of-range
  placings, entry-count edges) and `StandingsCalculator` (ties, top-N, min-shows, each basis).
- **Import**: fixture-driven. `tests/HorseShow.Import.Tests/Fixtures/` holds real-shaped
  pastes and files (one per known source format). Each fixture has an expected JSON
  snapshot of the parsed batch. Adding support for a new show-software format means
  adding a fixture, which doubles as documentation.
- **Data**: SQLite temp-file tests for migrations-from-empty, alias uniqueness, cascade
  behaviour on undo-import, and `VACUUM INTO` backup/restore round trip.
- **UI**: bUnit for the mapping grid and resolution grid only; those have real logic.
  Pages with trivial logic are not worth component tests.
- **CI**: GitHub Actions on `windows-latest`: install MAUI workload, `dotnet build`,
  `dotnet test` on the non-MAUI projects. The MAUI project builds but is not run in CI.

---

## 10. Phases

Each phase ends in a working, committed app.

**Phase 0: Skeleton** (done)
Solution, projects, CPM, CI workflow, MudBlazor shell with nav, SQLite + EF with an
initial migration, Web dev host wired to the same RCL. App launches on Windows and
shows an empty dashboard.

**Phase 1: Core data and manual entry** (done)
Seasons, shows, classes, riders, horses, results with inline editing. Built-in point
schedules seeded. No standings yet. This is already usable for someone willing to type.
As built: a quick-add row per class (Tab across, Enter adds, placing auto-increments),
MudTable row editing for corrections, rider/horse autocompletes that offer "Create" when
the typed name is unknown and resolve through the alias tables otherwise.

**Phase 2: Scoring and standings** (done)
`PointsCalculator`, `StandingsCalculator`, standings page with filters and drill-down,
recalculate command with diff. Schedule editor.
As built: points are recomputed for the whole class on every result or class save (the
row count stands in for a missing entry count, so one new row can move everyone under an
entry-scaled schedule); changing a season's or division's schedule, or editing or deleting
a schedule, recalculates the affected seasons at once; the explicit Recalculate command on
the standings page shows a diff first. Typing a points value in the results grid marks the
row Manual and the engine leaves it alone; clearing it hands the row back.

**Phase 3: Import, paste first** (done, and already covers XLSX)
Tokenizer, structure detection, mapping grid, normalizer, name resolution, preview,
commit, undo. Paste and CSV only. Mapping profiles. This phase is the longest and gets
the fixture corpus started.
As built: CsvHelper for CSV and delimited paste, ClosedXML for XLSX (sheet picker when
there are several), FuzzySharp token-sort ratio for name suggestions (86+ suggests,
70+ lists as a candidate), a `MudStepper` wizard. Imports into an existing show match
classes by number then name and offer skip / replace / add-anyway for duplicate rider+horse
rows. A confirmed fuzzy match writes the sheet's spelling as an alias. Undo removes the
batch's results plus any classes and show the batch created that are now empty. Built
without the real sheet, against six fixture shapes; the README describes the three places
a new format might need a line.

**Phase 4: Import, XLSX and ergonomics**
ClosedXML reader with sheet selection, wide-layout support if needed by then,
"Apply to all similar", combined rider/horse column splitting.

**Phase 5: Polish and distribution**
Export standings to CSV/XLSX and a print-friendly view, backup/restore, merge
duplicate riders/horses, settings, window state. MSIX packaging or a plain
self-contained publish with an installer (Inno Setup / Velopack if auto-update is wanted).

---

## 11. Open questions

Answers change the defaults; none of them block Phase 0 or 1.

1. **Which discipline/organization** are the results from (hunter/jumper, western,
   4-H, IEA, dressage, open schooling circuit)? This determines the seeded point
   schedules and whether numeric scores matter beyond placings.
2. **Standings basis**: by rider, by horse, or by rider+horse pair? The model supports
   all three; the question is only which should be the default.
3. **Do you have a sample of the results you paste?** One or two real examples would
   seed the fixture corpus and let the parser be built against actual formats rather
   than guesses. Which show-management software or websites publish them?
4. **Year-end rules**: top-N shows counted, minimum shows to qualify, drop-lowest? These
   are all cheap to add once known and awkward to retrofit into the standings logic later.
5. **Distribution**: just you on your own machine (unpackaged publish is enough), or
   other people (MSIX or an installer, and backup/restore matters more)?
