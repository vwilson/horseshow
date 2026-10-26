using System.Security.Cryptography;
using System.Text;
using HorseShow.Core.Model;
using HorseShow.Core.Text;
using HorseShow.Data;
using HorseShow.Import.Model;
using HorseShow.Import.Resolution;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Import;

public sealed record CommitSummary(int BatchId, int ShowId, int ResultsAdded, int ResultsReplaced, int ResultsSkipped, int ClassesCreated, int RidersCreated, int HorsesCreated);

/// <summary>Writes a plan in one transaction, tagging everything with an <see cref="ImportBatch"/> so it can be undone.</summary>
public static class ImportCommitter
{
    public static string Hash(string rawContent)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawContent))).ToLowerInvariant();

    public static async Task<CommitSummary> CommitAsync(
        HorseShowDbContext db, ImportPlan plan, ImportSource source, string? fileName, string? rawHash, int? mappingProfileId, CancellationToken ct = default)
    {
        if (plan.ErrorCount > 0 && plan.ImportableRowCount == 0)
        {
            throw new InvalidOperationException("Nothing to import: every row has an error.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var batch = new ImportBatch
        {
            SeasonId = plan.SeasonId,
            Source = source,
            FileName = fileName,
            ImportedAt = DateTime.UtcNow,
            RawContentHash = rawHash,
            MappingProfileId = mappingProfileId,
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);

        // Show
        Show show;
        if (plan.ShowId is int showId)
        {
            show = await db.Shows.FirstAsync(s => s.Id == showId, ct);
        }
        else
        {
            show = new Show
            {
                SeasonId = plan.SeasonId,
                Name = string.IsNullOrWhiteSpace(plan.NewShowName) ? $"Imported {DateTime.Today:yyyy-MM-dd}" : plan.NewShowName.Trim(),
                StartDate = plan.NewShowDate ?? DateOnly.FromDateTime(DateTime.Today),
                CreatedByImportBatchId = batch.Id,
            };
            db.Shows.Add(show);
            await db.SaveChangesAsync(ct);
        }

        batch.ShowId = show.Id;

        // Participants: decisions from the plan, created once per distinct name.
        var riderIds = await MaterializeAsync(db, plan.Riders, ParticipantKind.Rider, ct);
        var horseIds = await MaterializeAsync(db, plan.Horses, ParticipantKind.Horse, ct);
        var ridersCreated = plan.Riders.Count(r => r.CreatesNew);
        var horsesCreated = plan.Horses.Count(h => h.CreatesNew);

        int added = 0, replaced = 0, skipped = 0, classesCreated = 0;
        var touchedClasses = new List<int>();
        var nextSort = (await db.ShowClasses.Where(c => c.ShowId == show.Id).MaxAsync(c => (int?)c.SortOrder, ct) ?? 0) + 10;

        foreach (var planned in plan.Classes)
        {
            if (!planned.Rows.Any(r => r.WillImport))
            {
                continue;
            }

            ShowClass cls;
            if (planned.ExistingClassId is int existingId)
            {
                cls = await db.ShowClasses.Include(c => c.Results).FirstAsync(c => c.Id == existingId, ct);
                cls.EntryCount ??= planned.EntryCount;
            }
            else
            {
                cls = new ShowClass
                {
                    ShowId = show.Id,
                    ClassNumber = planned.ClassNumber,
                    Name = planned.Name ?? (planned.ClassNumber is null ? "Imported results" : $"Class {planned.ClassNumber}"),
                    EntryCount = planned.EntryCount,
                    SortOrder = nextSort,
                    CreatedByImportBatchId = batch.Id,
                };
                nextSort += 10;
                db.ShowClasses.Add(cls);
                await db.SaveChangesAsync(ct);
                classesCreated++;
            }

            touchedClasses.Add(cls.Id);

            foreach (var row in planned.Rows.Where(r => r.WillImport))
            {
                var riderId = row.RiderText is null ? null : riderIds.GetValueOrDefault(NameNormalizer.Normalize(row.RiderText));
                var horseId = row.HorseText is null ? null : horseIds.GetValueOrDefault(NameNormalizer.Normalize(row.HorseText));
                if (riderId is null && horseId is null)
                {
                    skipped++;
                    continue;
                }

                var duplicate = cls.Results.FirstOrDefault(r => r.RiderId == riderId && r.HorseId == horseId && r.ImportBatchId != batch.Id);
                if (duplicate is not null)
                {
                    switch (plan.DuplicatePolicy)
                    {
                        case DuplicatePolicy.Skip:
                            skipped++;
                            continue;
                        case DuplicatePolicy.Replace:
                            db.Results.Remove(duplicate);
                            cls.Results.Remove(duplicate);
                            replaced++;
                            break;
                    }
                }

                var result = new Result
                {
                    ShowClassId = cls.Id,
                    RiderId = riderId,
                    HorseId = horseId,
                    BackNumber = row.BackNumber,
                    Placing = row.Status == PlacingStatus.Placed ? row.Placing : null,
                    PlacingStatus = row.Status,
                    Score = row.Score,
                    Points = row.Points,
                    PointsSource = row.Points is null ? PointsSource.Computed : PointsSource.Imported,
                    ImportBatchId = batch.Id,
                };
                db.Results.Add(result);
                cls.Results.Add(result);
                if (duplicate is null)
                {
                    added++;
                }
            }

            await db.SaveChangesAsync(ct);
        }

        foreach (var classId in touchedClasses)
        {
            await PointsRecalculator.RecalculateClassAsync(db, classId, ct);
        }

        batch.RowCount = added + replaced;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new CommitSummary(batch.Id, show.Id, added, replaced, skipped, classesCreated, ridersCreated, horsesCreated);
    }

    /// <summary>
    /// Applies the resolution decisions: existing ids are reused and the source spelling is added as an
    /// alias so the next import matches exactly; new names are created through <see cref="ParticipantLookup"/>.
    /// </summary>
    private static async Task<Dictionary<string, int?>> MaterializeAsync(HorseShowDbContext db, List<NameResolution> resolutions, ParticipantKind kind, CancellationToken ct)
    {
        var map = new Dictionary<string, int?>();
        foreach (var r in resolutions)
        {
            if (r.ChosenId is int id)
            {
                map[r.Normalized] = id;
                if (r.Status != ResolutionStatus.Exact)
                {
                    if (kind == ParticipantKind.Rider && !await db.RiderAliases.AnyAsync(a => a.Alias == r.Normalized, ct))
                    {
                        db.RiderAliases.Add(new RiderAlias { RiderId = id, Alias = r.Normalized });
                    }
                    else if (kind == ParticipantKind.Horse && !await db.HorseAliases.AnyAsync(a => a.Alias == r.Normalized, ct))
                    {
                        db.HorseAliases.Add(new HorseAlias { HorseId = id, Alias = r.Normalized });
                    }
                }
            }
            else
            {
                if (kind == ParticipantKind.Rider)
                {
                    var rider = await ParticipantLookup.GetOrCreateRiderAsync(db, r.Text, ct);
                    await db.SaveChangesAsync(ct);
                    map[r.Normalized] = rider.Id;
                }
                else
                {
                    var horse = await ParticipantLookup.GetOrCreateHorseAsync(db, r.Text, ct);
                    await db.SaveChangesAsync(ct);
                    map[r.Normalized] = horse.Id;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return map;
    }

    /// <summary>
    /// Removes the batch's results, then any classes and the show the batch created that are now empty.
    /// Riders, horses and aliases stay; they are harmless and probably correct.
    /// </summary>
    public static async Task<(int Results, int Classes, bool Show)> UndoAsync(HorseShowDbContext db, int batchId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var classIds = await db.Results.Where(r => r.ImportBatchId == batchId).Select(r => r.ShowClassId).Distinct().ToListAsync(ct);
        var results = await db.Results.Where(r => r.ImportBatchId == batchId).ExecuteDeleteAsync(ct);

        var classes = await db.ShowClasses
            .Where(c => c.CreatedByImportBatchId == batchId && !c.Results.Any())
            .ExecuteDeleteAsync(ct);

        var showDeleted = await db.Shows
            .Where(s => s.CreatedByImportBatchId == batchId && !s.Classes.Any())
            .ExecuteDeleteAsync(ct) > 0;

        foreach (var classId in classIds)
        {
            await PointsRecalculator.RecalculateClassAsync(db, classId, ct);
        }

        await db.SaveChangesAsync(ct);
        await db.ImportBatches.Where(b => b.Id == batchId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        return (results, classes, showDeleted);
    }
}
