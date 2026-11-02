namespace HorseShow.Core.Model;

/// <summary>Audit record for one import. Results reference it so an import can be undone as a unit.</summary>
public class ImportBatch
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season? Season { get; set; }
    public int? ShowId { get; set; }
    public Show? Show { get; set; }

    public ImportSource Source { get; set; }
    public string? FileName { get; set; }
    public DateTime ImportedAt { get; set; }
    public int RowCount { get; set; }

    /// <summary>SHA-256 of the raw input, used to warn on re-importing the same thing.</summary>
    public string? RawContentHash { get; set; }

    public int? MappingProfileId { get; set; }
    public MappingProfile? MappingProfile { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A saved column mapping for a known results source.</summary>
public class MappingProfile
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? SourceHint { get; set; }
    public string ColumnMapJson { get; set; } = "{}";
    public string ParserOptionsJson { get; set; } = "{}";
}
