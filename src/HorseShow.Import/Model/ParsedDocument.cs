namespace HorseShow.Import.Model;

public enum RowKind { Noise, Header, Section, Data }

/// <summary>A class header line found in the source, e.g. "Class 12 - Hunter Under Saddle (8 entries)".</summary>
public sealed record ClassHeader(string? Number, string? Name, int? EntryCount, int LineNumber)
{
    public string Key => !string.IsNullOrEmpty(Number) ? $"#{Number}" : Name ?? $"line {LineNumber}";
    public string Display => string.Join(" ", new[] { Number is null ? null : $"#{Number}", Name }.Where(s => s is not null));
}

/// <summary>Data rows under one class header. A source with no headers has a single block with a null header.</summary>
public sealed class ClassBlock
{
    public ClassHeader? Header { get; init; }
    public List<RawRow> Rows { get; } = [];
}

/// <summary>The source after row classification: an optional header row, column samples, and class blocks.</summary>
public sealed class ParsedDocument
{
    public required RawTable Table { get; init; }
    public RawRow? HeaderRow { get; init; }
    public int ColumnCount { get; init; }
    public List<ClassBlock> Blocks { get; } = [];
    public IReadOnlyList<(int LineNumber, RowKind Kind)> Classification { get; init; } = [];

    public int DataRowCount => Blocks.Sum(b => b.Rows.Count);

    public string? HeaderText(int column)
        => HeaderRow is not null && column < HeaderRow.Cells.Length ? HeaderRow.Cells[column].Trim() : null;

    public IEnumerable<string> Samples(int column, int take = 4)
        => Blocks.SelectMany(b => b.Rows)
            .Select(r => column < r.Cells.Length ? r.Cells[column].Trim() : string.Empty)
            .Where(s => s.Length > 0)
            .Distinct()
            .Take(take);
}
