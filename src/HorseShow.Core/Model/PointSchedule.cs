namespace HorseShow.Core.Model;

public class PointSchedule
{
    public int Id { get; set; }

    /// <summary>Null for built-in schedules shared across seasons.</summary>
    public int? SeasonId { get; set; }
    public Season? Season { get; set; }

    public required string Name { get; set; }
    public PointScheduleMode Mode { get; set; } = PointScheduleMode.Fixed;
    public bool IsBuiltIn { get; set; }

    public ICollection<PointScheduleRow> Rows { get; set; } = [];
}

public class PointScheduleRow
{
    public int Id { get; set; }
    public int PointScheduleId { get; set; }
    public PointSchedule? PointSchedule { get; set; }

    public int Placing { get; set; }
    public decimal Points { get; set; }

    /// <summary>
    /// For <see cref="PointScheduleMode.ByEntryCount"/>: this row applies when the class has at least
    /// this many entries. The row with the highest MinEntries not exceeding the class entry count wins.
    /// Zero for <see cref="PointScheduleMode.Fixed"/>.
    /// </summary>
    public int MinEntries { get; set; }
}
