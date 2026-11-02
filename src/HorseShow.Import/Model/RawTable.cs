namespace HorseShow.Import.Model;

/// <summary>A row of cells straight from the source, before any interpretation.</summary>
public sealed record RawRow(int LineNumber, string[] Cells)
{
    public int NonEmptyCount => Cells.Count(c => !string.IsNullOrWhiteSpace(c));
    public string Joined => string.Join(" ", Cells.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()));
}

/// <summary>Cells and nothing else. Produced by the readers, consumed by the classifier.</summary>
public sealed class RawTable
{
    public List<RawRow> Rows { get; init; } = [];
    public string? DelimiterName { get; init; }
    public string? SheetName { get; init; }
    public IReadOnlyList<string> SheetNames { get; init; } = [];
    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(r => r.Cells.Length);
}
