using HorseShow.Core.Model;
using HorseShow.Import.Resolution;

namespace HorseShow.Import.Model;

/// <summary>One result as it will be written, with the problems found on the way.</summary>
public sealed class PlannedRow
{
    public int LineNumber { get; init; }
    public int? Placing { get; set; }
    public PlacingStatus Status { get; set; } = PlacingStatus.Placed;
    public string? BackNumber { get; set; }
    public string? RiderText { get; set; }
    public string? HorseText { get; set; }
    public string? OwnerText { get; set; }
    public decimal? Score { get; set; }
    public decimal? Points { get; set; }
    public List<ImportIssue> Issues { get; } = [];
    /// <summary>Unticked rows are skipped at commit. Rows with errors are forced off.</summary>
    public bool Include { get; set; } = true;

    public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
    public bool WillImport => Include && !HasErrors;
}

public sealed class PlannedClass
{
    public required string Key { get; init; }
    public string? ClassNumber { get; set; }
    public string? Name { get; set; }
    public int? EntryCount { get; set; }
    /// <summary>Set when a class in the target show already matches by number or name.</summary>
    public int? ExistingClassId { get; set; }
    public string? ExistingClassName { get; set; }
    public int ExistingResultCount { get; set; }
    public List<PlannedRow> Rows { get; } = [];
    public List<ImportIssue> Issues { get; } = [];

    public string Display => string.Join(" ", new[] { ClassNumber is null ? null : $"#{ClassNumber}", Name }.Where(s => s is not null)) is { Length: > 0 } s ? s : Key;
}

public enum DuplicatePolicy { Skip, Replace, AddAnyway }

/// <summary>Everything the preview shows and the committer writes.</summary>
public sealed class ImportPlan
{
    public required int SeasonId { get; init; }
    public int? ShowId { get; set; }
    public string? NewShowName { get; set; }
    public DateOnly? NewShowDate { get; set; }

    public List<PlannedClass> Classes { get; } = [];
    public List<NameResolution> Riders { get; } = [];
    public List<NameResolution> Horses { get; } = [];
    public List<ImportIssue> Issues { get; } = [];
    public DuplicatePolicy DuplicatePolicy { get; set; } = DuplicatePolicy.Skip;

    public int RowCount => Classes.Sum(c => c.Rows.Count);
    public int ImportableRowCount => Classes.Sum(c => c.Rows.Count(r => r.WillImport));
    public int ErrorCount => Classes.Sum(c => c.Rows.Count(r => r.HasErrors)) + Issues.Count(i => i.Severity == IssueSeverity.Error);
    public int WarningCount => Classes.Sum(c => c.Rows.Sum(r => r.Issues.Count(i => i.Severity == IssueSeverity.Warning))) + Issues.Count(i => i.Severity == IssueSeverity.Warning);
    public bool HasPointsColumn => Classes.Any(c => c.Rows.Any(r => r.Points is not null));
}
