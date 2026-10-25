namespace HorseShow.Core.Model;

/// <summary>A standings bucket within a season, e.g. "Walk-Trot 11 &amp; Under".</summary>
public class Division
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season? Season { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Overrides the season's default schedule when set.</summary>
    public int? PointScheduleId { get; set; }
    public PointSchedule? PointSchedule { get; set; }
}
