namespace HorseShow.Core.Model;

public class Rider
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Barn { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }

    public ICollection<RiderAlias> Aliases { get; set; } = [];
}

public class RiderAlias
{
    public int Id { get; set; }
    public int RiderId { get; set; }
    public Rider? Rider { get; set; }

    /// <summary>Normalized form (see <see cref="Text.NameNormalizer"/>). Unique across riders.</summary>
    public required string Alias { get; set; }
}
