namespace HorseShow.Core.Model;

/// <summary>What a season aggregates standings by.</summary>
public enum StandingsBasis
{
    Rider = 0,
    Horse = 1,
    Combination = 2,
}

/// <summary>What kind of result a class produces.</summary>
public enum ClassType
{
    /// <summary>Placings only (most western, 4-H, open circuits).</summary>
    Placed = 0,
    /// <summary>Numeric score only (dressage percentages, some hunter classes).</summary>
    Scored = 1,
    /// <summary>Both a judge's score and a placing.</summary>
    Both = 2,
}

public enum PlacingStatus
{
    Placed = 0,
    NoPlace = 1,
    Scratched = 2,
    Disqualified = 3,
    Eliminated = 4,
}

/// <summary>Where a result's points came from. Only Computed points are overwritten by recalculation.</summary>
public enum PointsSource
{
    Computed = 0,
    Imported = 1,
    Manual = 2,
}

public enum PointScheduleMode
{
    /// <summary>Placing maps to points regardless of entries.</summary>
    Fixed = 0,
    /// <summary>Placing maps to points, with rows selected by minimum entry count.</summary>
    ByEntryCount = 1,
}

public enum ImportSource
{
    Paste = 0,
    Csv = 1,
    Xlsx = 2,
}
