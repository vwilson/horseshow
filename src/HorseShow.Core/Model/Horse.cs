namespace HorseShow.Core.Model;

public class Horse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Owner { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }

    public ICollection<HorseAlias> Aliases { get; set; } = [];
}

public class HorseAlias
{
    public int Id { get; set; }
    public int HorseId { get; set; }
    public Horse? Horse { get; set; }

    /// <summary>Normalized form (see <see cref="Text.NameNormalizer"/>). Unique across horses.</summary>
    public required string Alias { get; set; }
}
