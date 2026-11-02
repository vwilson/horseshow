using HorseShow.Core.Model;
using HorseShow.Data;
using HorseShow.Import;
using HorseShow.Import.Mapping;
using HorseShow.Import.Model;
using HorseShow.Import.Resolution;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.UI.Services;

/// <summary>Wizard state. Scoped, so it survives step changes and dies with the circuit/window.</summary>
public sealed class ImportSession
{
    public ImportSource Source { get; set; } = ImportSource.Paste;
    public string? RawText { get; set; }
    public byte[]? FileBytes { get; set; }
    public string? FileName { get; set; }
    public string? SheetName { get; set; }

    public int? ShowId { get; set; }
    public string? NewShowName { get; set; }
    public DateTime? NewShowDate { get; set; } = DateTime.Today;

    public ParsedDocument? Document { get; set; }
    public ColumnMapping? Mapping { get; set; }
    public int? ProfileId { get; set; }
    public ImportPlan? Plan { get; set; }
    public CommitSummary? Summary { get; set; }

    public bool HasInput => Source == ImportSource.Paste ? !string.IsNullOrWhiteSpace(RawText) : FileBytes is { Length: > 0 };

    public void Reset()
    {
        RawText = null; FileBytes = null; FileName = null; SheetName = null;
        Document = null; Mapping = null; ProfileId = null; Plan = null; Summary = null;
        ShowId = null; NewShowName = null; NewShowDate = DateTime.Today;
    }
}

public sealed record ImportBatchRow(ImportBatch Batch, string? ShowName, int ResultCount, int ClassesCreated);

public sealed class ImportViewService(IDbContextFactory<HorseShowDbContext> factory)
{
    private readonly ImportPlanner _planner = new(new NameResolver());

    // ---- parse ----

    public ParsedDocument Parse(ImportSession s)
    {
        if (s.Source == ImportSource.Paste)
        {
            return ImportPipeline.ParseText(s.RawText ?? string.Empty);
        }

        using var stream = new MemoryStream(s.FileBytes ?? []);
        var name = s.FileName ?? "results.csv";
        return Path.GetExtension(name).ToLowerInvariant() is ".xlsx" or ".xlsm"
            ? ImportPipeline.ParseXlsx(stream, s.SheetName)
            : ImportPipeline.ParseCsv(stream);
    }

    public static ImportSource SourceFor(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() is ".xlsx" or ".xlsm" ? ImportSource.Xlsx : ImportSource.Csv;

    // ---- profiles ----

    public async Task<List<MappingProfile>> ListProfilesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.MappingProfiles.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<MappingProfile> SaveProfileAsync(string name, ColumnMapping mapping, ParsedDocument doc, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var trimmed = name.Trim();
        var profile = await db.MappingProfiles.FirstOrDefaultAsync(p => p.Name == trimmed, ct)
                      ?? db.MappingProfiles.Add(new MappingProfile { Name = trimmed }).Entity;
        profile.ColumnMapJson = MappingProfileData.Capture(mapping, doc).ToJson();
        profile.SourceHint = doc.Table.DelimiterName;
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task DeleteProfileAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.MappingProfiles.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
    }

    public static ColumnMapping ApplyProfile(MappingProfile profile, ParsedDocument doc)
        => MappingProfileData.FromJson(profile.ColumnMapJson).Apply(doc);

    // ---- plan and commit ----

    public async Task<ImportPlan> BuildPlanAsync(ImportSession s, int seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var plan = await _planner.BuildAsync(db, s.Document!, s.Mapping!, seasonId, s.ShowId, ct);
        plan.NewShowName = s.NewShowName;
        plan.NewShowDate = s.NewShowDate is null ? null : DateOnly.FromDateTime(s.NewShowDate.Value);
        if (s.RawText is not null)
        {
            var hash = ImportCommitter.Hash(s.RawText);
            if (await db.ImportBatches.AnyAsync(b => b.RawContentHash == hash, ct))
            {
                plan.Issues.Add(ImportIssue.Warning("This exact text was imported before. Importing again may create duplicates."));
            }
        }

        return plan;
    }

    public async Task<CommitSummary> CommitAsync(ImportSession s, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var hash = s.RawText is null ? null : ImportCommitter.Hash(s.RawText);
        return await ImportCommitter.CommitAsync(db, s.Plan!, s.Source, s.FileName, hash, s.ProfileId, ct);
    }

    // ---- history ----

    public async Task<List<ImportBatchRow>> ListBatchesAsync(int seasonId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ImportBatches.AsNoTracking()
            .Where(b => b.SeasonId == seasonId)
            .OrderByDescending(b => b.ImportedAt)
            .Select(b => new ImportBatchRow(
                b,
                b.Show != null ? b.Show.Name : null,
                db.Results.Count(r => r.ImportBatchId == b.Id),
                db.ShowClasses.Count(c => c.CreatedByImportBatchId == b.Id)))
            .ToListAsync(ct);
    }

    public async Task<(int Results, int Classes, bool Show)> UndoAsync(int batchId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await ImportCommitter.UndoAsync(db, batchId, ct);
    }
}
