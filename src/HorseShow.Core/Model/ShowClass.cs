namespace HorseShow.Core.Model;

public class ShowClass
{
    public int Id { get; set; }
    public int ShowId { get; set; }
    public Show? Show { get; set; }
    public int? DivisionId { get; set; }
    public Division? Division { get; set; }

    public string? ClassNumber { get; set; }
    public required string Name { get; set; }
    public ClassType ClassType { get; set; } = ClassType.Placed;

    /// <summary>
    /// Number of entries that competed. Kept separately from the result rows because
    /// published results often list only the top placings.
    /// </summary>
    public int? EntryCount { get; set; }

    public string? JudgeName { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Set when an import created this class, so undoing that import can remove it if still empty.</summary>
    public int? CreatedByImportBatchId { get; set; }

    public ICollection<Result> Results { get; set; } = [];
}
