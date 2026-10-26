using HorseShow.Core.Model;
using HorseShow.Core.Text;
using HorseShow.Data;
using HorseShow.Import.Mapping;
using HorseShow.Import.Model;
using HorseShow.Import.Normalization;
using HorseShow.Import.Resolution;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Import;

/// <summary>
/// Document + mapping → plan. Normalizes every cell, groups rows into classes, matches classes
/// already in the target show, resolves names, and attaches warnings and errors per row.
/// </summary>
public sealed class ImportPlanner(NameResolver resolver)
{
    public async Task<ImportPlan> BuildAsync(HorseShowDbContext db, ParsedDocument doc, ColumnMapping mapping, int seasonId, int? showId, CancellationToken ct = default)
    {
        var plan = new ImportPlan { SeasonId = seasonId, ShowId = showId };
        var classesByKey = new Dictionary<string, PlannedClass>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in doc.Blocks)
        {
            foreach (var raw in block.Rows)
            {
                var row = Normalize(raw, mapping);
                var cls = ClassFor(plan, classesByKey, block, raw, mapping);
                cls.Rows.Add(row);
            }
        }

        foreach (var cls in plan.Classes)
        {
            Validate(cls);
        }

        if (showId is int sid)
        {
            await MatchExistingClassesAsync(db, plan, sid, ct);
        }

        plan.Riders.AddRange(await resolver.ResolveAsync(db, ParticipantKind.Rider, plan.Classes.SelectMany(c => c.Rows).Where(r => r.RiderText is not null).Select(r => r.RiderText!), ct));
        plan.Horses.AddRange(await resolver.ResolveAsync(db, ParticipantKind.Horse, plan.Classes.SelectMany(c => c.Rows).Where(r => r.HorseText is not null).Select(r => r.HorseText!), ct));

        if (plan.Classes.Count == 0)
        {
            plan.Issues.Add(ImportIssue.Error("No result rows were found."));
        }

        return plan;
    }

    private static PlannedRow Normalize(RawRow raw, ColumnMapping mapping)
    {
        string? Cell(ImportField f)
        {
            var col = mapping.ColumnFor(f);
            if (col is null || col >= raw.Cells.Length)
            {
                return null;
            }

            var v = raw.Cells[col.Value].Trim();
            return v.Length == 0 ? null : v;
        }

        var row = new PlannedRow { LineNumber = raw.LineNumber };

        var placingText = Cell(ImportField.Placing);
        if (placingText is not null)
        {
            var parsed = PlacingParser.Parse(placingText);
            if (parsed is null)
            {
                row.Issues.Add(ImportIssue.Error($"Could not read placing \"{placingText}\"."));
            }
            else
            {
                row.Placing = parsed.Value.Placing;
                row.Status = parsed.Value.Status;
            }
        }
        else if (mapping.Has(ImportField.Placing))
        {
            row.Status = PlacingStatus.NoPlace;
            row.Issues.Add(ImportIssue.Warning("No placing; recorded as no place."));
        }

        row.BackNumber = Cell(ImportField.BackNumber);
        row.RiderText = Cell(ImportField.Rider);
        row.HorseText = Cell(ImportField.Horse);
        row.OwnerText = Cell(ImportField.Owner);

        var combined = Cell(ImportField.RiderAndHorse);
        if (combined is not null)
        {
            var split = RiderHorseSplitter.Split(combined);
            if (split is null)
            {
                row.RiderText ??= combined;
                row.Issues.Add(ImportIssue.Warning($"Could not split \"{combined}\" into rider and horse; used it as the rider."));
            }
            else
            {
                row.RiderText ??= split.Value.Rider;
                row.HorseText ??= split.Value.Horse;
            }
        }

        var scoreText = Cell(ImportField.Score);
        if (scoreText is not null)
        {
            row.Score = ScoreParser.Parse(scoreText);
            if (row.Score is null)
            {
                row.Issues.Add(ImportIssue.Warning($"Could not read score \"{scoreText}\"."));
            }
        }

        var pointsText = Cell(ImportField.Points);
        if (pointsText is not null)
        {
            row.Points = ScoreParser.Parse(pointsText);
            if (row.Points is null)
            {
                row.Issues.Add(ImportIssue.Warning($"Could not read points \"{pointsText}\"."));
            }
        }

        if (row.RiderText is null && row.HorseText is null)
        {
            row.Issues.Add(ImportIssue.Error("No rider or horse on this row."));
        }

        if (row.Status == PlacingStatus.Placed && row.Placing is null && mapping.Has(ImportField.Placing))
        {
            row.Issues.Add(ImportIssue.Error("No placing."));
        }

        return row;
    }

    private static PlannedClass ClassFor(ImportPlan plan, Dictionary<string, PlannedClass> byKey, ClassBlock block, RawRow raw, ColumnMapping mapping)
    {
        string? number = null, name = null;
        int? entries = null;

        var numCol = mapping.ColumnFor(ImportField.ClassNumber);
        var nameCol = mapping.ColumnFor(ImportField.ClassName);
        var entriesCol = mapping.ColumnFor(ImportField.EntryCount);
        if (numCol is int nc && nc < raw.Cells.Length && raw.Cells[nc].Trim() is { Length: > 0 } n)
        {
            // "12 - Hunter Under Saddle" in one cell is common; split it.
            var parsedCell = ClassHeaderParserBridge(n);
            number = parsedCell?.Number ?? n;
            name = parsedCell?.Name;
        }

        if (nameCol is int nmc && nmc < raw.Cells.Length && raw.Cells[nmc].Trim() is { Length: > 0 } nm)
        {
            name = nm;
        }

        if (entriesCol is int ec && ec < raw.Cells.Length)
        {
            entries = EntryCountParser.Parse(raw.Cells[ec]);
        }

        if (number is null && name is null && block.Header is not null)
        {
            number = block.Header.Number;
            name = block.Header.Name;
            entries ??= block.Header.EntryCount;
        }
        else if (block.Header is not null)
        {
            entries ??= block.Header.EntryCount;
        }

        var key = number is not null ? $"#{number}" : name is not null ? NameNormalizer.Normalize(name) : block.Header?.Key ?? "(all rows)";
        if (!byKey.TryGetValue(key, out var cls))
        {
            cls = new PlannedClass { Key = key, ClassNumber = number, Name = name ?? (number is null ? "Imported results" : null), EntryCount = entries };
            byKey[key] = cls;
            plan.Classes.Add(cls);
        }
        else
        {
            cls.Name ??= name;
            cls.EntryCount ??= entries;
        }

        return cls;
    }

    private static ClassHeader? ClassHeaderParserBridge(string cell)
    {
        var h = Parsing.ClassHeaderParser.Parse(cell, 0);
        return h is { Number: not null } ? h : null;
    }

    private static void Validate(PlannedClass cls)
    {
        var placed = cls.Rows.Where(r => r.Status == PlacingStatus.Placed && r.Placing is not null).ToList();
        foreach (var dup in placed.GroupBy(r => r.Placing).Where(g => g.Count() > 1))
        {
            foreach (var r in dup)
            {
                r.Issues.Add(ImportIssue.Warning($"Placing {dup.Key} appears {dup.Count()} times in this class."));
            }
        }

        if (cls.EntryCount is int entries)
        {
            foreach (var r in placed.Where(r => r.Placing > entries))
            {
                r.Issues.Add(ImportIssue.Warning($"Placing {r.Placing} is above the {entries} entries recorded."));
            }
        }

        var seen = new HashSet<string>();
        foreach (var r in cls.Rows)
        {
            var who = $"{NameNormalizer.Normalize(r.RiderText)}|{NameNormalizer.Normalize(r.HorseText)}";
            if (who != "|" && !seen.Add(who))
            {
                r.Issues.Add(ImportIssue.Warning("Same rider and horse appear twice in this class."));
            }
        }
    }

    /// <summary>Marks classes that already exist in the show, by number first, then by normalized name.</summary>
    private static async Task MatchExistingClassesAsync(HorseShowDbContext db, ImportPlan plan, int showId, CancellationToken ct)
    {
        var existing = await db.ShowClasses.AsNoTracking()
            .Where(c => c.ShowId == showId)
            .Select(c => new { c.Id, c.ClassNumber, c.Name, ResultCount = c.Results.Count })
            .ToListAsync(ct);

        foreach (var cls in plan.Classes)
        {
            var match = cls.ClassNumber is not null
                ? existing.FirstOrDefault(e => string.Equals(e.ClassNumber, cls.ClassNumber, StringComparison.OrdinalIgnoreCase))
                : null;
            match ??= cls.Name is not null
                ? existing.FirstOrDefault(e => NameNormalizer.Normalize(e.Name) == NameNormalizer.Normalize(cls.Name))
                : null;
            if (match is null)
            {
                continue;
            }

            cls.ExistingClassId = match.Id;
            cls.ExistingClassName = match.Name;
            cls.ExistingResultCount = match.ResultCount;
            if (match.ResultCount > 0)
            {
                cls.Issues.Add(ImportIssue.Warning($"Class already has {match.ResultCount} result(s). Duplicates are handled by the policy below."));
            }
        }
    }
}
