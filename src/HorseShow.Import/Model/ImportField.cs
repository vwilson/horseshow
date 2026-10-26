namespace HorseShow.Import.Model;

/// <summary>What a source column means. The mapping step assigns one of these to each column.</summary>
public enum ImportField
{
    Ignore = 0,
    ClassNumber,
    ClassName,
    Placing,
    BackNumber,
    Rider,
    Horse,
    /// <summary>One cell holding both, e.g. "Jane Smith on Blue Moon". Split during normalization.</summary>
    RiderAndHorse,
    Owner,
    Score,
    Points,
    EntryCount,
}

public static class ImportFieldInfo
{
    public static string Label(ImportField f) => f switch
    {
        ImportField.Ignore => "Ignore",
        ImportField.ClassNumber => "Class #",
        ImportField.ClassName => "Class name",
        ImportField.Placing => "Placing",
        ImportField.BackNumber => "Back #",
        ImportField.Rider => "Rider",
        ImportField.Horse => "Horse",
        ImportField.RiderAndHorse => "Rider and horse (one cell)",
        ImportField.Owner => "Owner",
        ImportField.Score => "Score",
        ImportField.Points => "Points (as published)",
        ImportField.EntryCount => "Entries in class",
        _ => f.ToString(),
    };
}
