namespace HorseShow.Core.Model;

public class Season
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Year { get; set; }
    public string? Organization { get; set; }
    public bool IsActive { get; set; }
    public StandingsBasis StandingsBasis { get; set; } = StandingsBasis.Rider;

    /// <summary>Only the best N shows count toward standings. Null means all shows count.</summary>
    public int? TopNShowsCounted { get; set; }

    /// <summary>Minimum shows attended to appear in year-end standings. Null means no minimum.</summary>
    public int? MinShowsForAward { get; set; }

    public int? DefaultPointScheduleId { get; set; }
    public PointSchedule? DefaultPointSchedule { get; set; }

    public ICollection<Show> Shows { get; set; } = [];
    public ICollection<Division> Divisions { get; set; } = [];
}
