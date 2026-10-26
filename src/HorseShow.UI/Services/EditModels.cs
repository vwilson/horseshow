using HorseShow.Core.Model;

namespace HorseShow.UI.Services;

// Plain edit models for dialogs and inline editors. Keeping these separate from the entities
// means a cancelled dialog never leaves a half-edited object in a list, and the view services
// own the mapping in one place.

public sealed class SeasonEdit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; } = DateTime.Today.Year;
    public string? Organization { get; set; }
    public StandingsBasis StandingsBasis { get; set; } = StandingsBasis.Rider;
    public int? TopNShowsCounted { get; set; }
    public int? MinShowsForAward { get; set; }
    public int? DefaultPointScheduleId { get; set; }

    public static SeasonEdit From(Season s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Year = s.Year,
        Organization = s.Organization,
        StandingsBasis = s.StandingsBasis,
        TopNShowsCounted = s.TopNShowsCounted,
        MinShowsForAward = s.MinShowsForAward,
        DefaultPointScheduleId = s.DefaultPointScheduleId,
    };

    public void ApplyTo(Season s)
    {
        s.Name = Name.Trim();
        s.Year = Year;
        s.Organization = string.IsNullOrWhiteSpace(Organization) ? null : Organization.Trim();
        s.StandingsBasis = StandingsBasis;
        s.TopNShowsCounted = TopNShowsCounted;
        s.MinShowsForAward = MinShowsForAward;
        s.DefaultPointScheduleId = DefaultPointScheduleId;
    }
}

public sealed class DivisionEdit
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? PointScheduleId { get; set; }

    public static DivisionEdit From(Division d) => new()
    {
        Id = d.Id, SeasonId = d.SeasonId, Name = d.Name, SortOrder = d.SortOrder, PointScheduleId = d.PointScheduleId,
    };

    public void ApplyTo(Division d)
    {
        d.SeasonId = SeasonId;
        d.Name = Name.Trim();
        d.SortOrder = SortOrder;
        d.PointScheduleId = PointScheduleId;
    }
}

public sealed class ShowEdit
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public string? Venue { get; set; }
    public string? Notes { get; set; }

    public static ShowEdit From(Show s) => new()
    {
        Id = s.Id,
        SeasonId = s.SeasonId,
        Name = s.Name,
        StartDate = s.StartDate.ToDateTime(TimeOnly.MinValue),
        EndDate = s.EndDate?.ToDateTime(TimeOnly.MinValue),
        Venue = s.Venue,
        Notes = s.Notes,
    };

    public void ApplyTo(Show s)
    {
        s.SeasonId = SeasonId;
        s.Name = Name.Trim();
        s.StartDate = DateOnly.FromDateTime(StartDate ?? DateTime.Today);
        s.EndDate = EndDate is null ? null : DateOnly.FromDateTime(EndDate.Value);
        s.Venue = string.IsNullOrWhiteSpace(Venue) ? null : Venue.Trim();
        s.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
    }
}

public sealed class ShowClassEdit
{
    public int Id { get; set; }
    public int ShowId { get; set; }
    public string? ClassNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? DivisionId { get; set; }
    public ClassType ClassType { get; set; } = ClassType.Placed;
    public int? EntryCount { get; set; }
    public string? JudgeName { get; set; }
    public string? Notes { get; set; }

    public static ShowClassEdit From(ShowClass c) => new()
    {
        Id = c.Id,
        ShowId = c.ShowId,
        ClassNumber = c.ClassNumber,
        Name = c.Name,
        DivisionId = c.DivisionId,
        ClassType = c.ClassType,
        EntryCount = c.EntryCount,
        JudgeName = c.JudgeName,
        Notes = c.Notes,
    };

    public void ApplyTo(ShowClass c)
    {
        c.ShowId = ShowId;
        c.ClassNumber = string.IsNullOrWhiteSpace(ClassNumber) ? null : ClassNumber.Trim();
        c.Name = Name.Trim();
        c.DivisionId = DivisionId;
        c.ClassType = ClassType;
        c.EntryCount = EntryCount;
        c.JudgeName = string.IsNullOrWhiteSpace(JudgeName) ? null : JudgeName.Trim();
        c.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
    }
}

public sealed class RiderEdit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Barn { get; set; }
    public string? Notes { get; set; }
    public List<string> Aliases { get; set; } = [];

    public static RiderEdit From(Rider r) => new()
    {
        Id = r.Id, Name = r.Name, DisplayName = r.DisplayName, Barn = r.Barn, Notes = r.Notes,
        Aliases = r.Aliases.Select(a => a.Alias).ToList(),
    };
}

public sealed class HorseEdit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Notes { get; set; }
    public List<string> Aliases { get; set; } = [];

    public static HorseEdit From(Horse h) => new()
    {
        Id = h.Id, Name = h.Name, Owner = h.Owner, RegistrationNumber = h.RegistrationNumber, Notes = h.Notes,
        Aliases = h.Aliases.Select(a => a.Alias).ToList(),
    };
}

/// <summary>An existing rider/horse (Id set) or a name to create (Id null).</summary>
public sealed record NameOption(int? Id, string Name)
{
    public bool IsNew => Id is null;
    public override string ToString() => Name;
}

/// <summary>Row model for the per-class results editor.</summary>
public sealed class ResultRow
{
    public int Id { get; set; }
    public int ShowClassId { get; set; }
    public int? Placing { get; set; }
    public PlacingStatus Status { get; set; } = PlacingStatus.Placed;
    public string? BackNumber { get; set; }
    public NameOption? Rider { get; set; }
    public NameOption? Horse { get; set; }
    public decimal? Score { get; set; }
    public decimal? Points { get; set; }
    public PointsSource PointsSource { get; set; }

    public static ResultRow From(Result r) => new()
    {
        Id = r.Id,
        ShowClassId = r.ShowClassId,
        Placing = r.Placing,
        Status = r.PlacingStatus,
        BackNumber = r.BackNumber,
        Rider = r.Rider is null ? null : new NameOption(r.Rider.Id, r.Rider.DisplayName ?? r.Rider.Name),
        Horse = r.Horse is null ? null : new NameOption(r.Horse.Id, r.Horse.Name),
        Score = r.Score,
        Points = r.Points,
        PointsSource = r.PointsSource,
    };

    public ResultRow Clone() => (ResultRow)MemberwiseClone();

    public void CopyFrom(ResultRow other)
    {
        Placing = other.Placing;
        Status = other.Status;
        BackNumber = other.BackNumber;
        Rider = other.Rider;
        Horse = other.Horse;
        Score = other.Score;
        Points = other.Points;
        PointsSource = other.PointsSource;
    }
}

public sealed class PointScheduleRowEdit
{
    public int Placing { get; set; } = 1;
    public decimal Points { get; set; }
    public int MinEntries { get; set; }
}

public sealed class PointScheduleEdit
{
    public int Id { get; set; }
    public int? SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PointScheduleMode Mode { get; set; } = PointScheduleMode.Fixed;
    public bool IsBuiltIn { get; set; }
    public List<PointScheduleRowEdit> Rows { get; set; } = [];

    public static PointScheduleEdit From(PointSchedule p) => new()
    {
        Id = p.Id,
        SeasonId = p.SeasonId,
        Name = p.Name,
        Mode = p.Mode,
        IsBuiltIn = p.IsBuiltIn,
        Rows = p.Rows.OrderBy(r => r.MinEntries).ThenBy(r => r.Placing)
            .Select(r => new PointScheduleRowEdit { Placing = r.Placing, Points = r.Points, MinEntries = r.MinEntries }).ToList(),
    };
}
