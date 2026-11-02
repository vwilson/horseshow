namespace HorseShow.Import.Resolution;

public enum ParticipantKind { Rider, Horse }

public enum ResolutionStatus
{
    /// <summary>An alias matched exactly. No prompt.</summary>
    Exact,
    /// <summary>A close match exists; the user should confirm.</summary>
    Suggested,
    /// <summary>Nothing close. Default is to create.</summary>
    New,
}

public sealed record Candidate(int Id, string Name, int Score);

/// <summary>One distinct name from the source and what it will map to.</summary>
public sealed class NameResolution
{
    public required ParticipantKind Kind { get; init; }
    public required string Text { get; init; }
    public required string Normalized { get; init; }
    public ResolutionStatus Status { get; init; }
    public List<Candidate> Candidates { get; init; } = [];
    public int Occurrences { get; set; }

    /// <summary>The user's (or default) decision: an existing id, or null to create.</summary>
    public int? ChosenId { get; set; }
    public string? ChosenName { get; set; }

    public bool CreatesNew => ChosenId is null;
}
