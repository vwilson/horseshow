namespace HorseShow.Core.Model;

/// <summary>
/// One line of a class's results. At least one of <see cref="RiderId"/> or <see cref="HorseId"/>
/// must be set; the database enforces this with a check constraint.
/// </summary>
public class Result
{
    public int Id { get; set; }
    public int ShowClassId { get; set; }
    public ShowClass? ShowClass { get; set; }

    public int? RiderId { get; set; }
    public Rider? Rider { get; set; }
    public int? HorseId { get; set; }
    public Horse? Horse { get; set; }

    public string? BackNumber { get; set; }

    public int? Placing { get; set; }
    public PlacingStatus PlacingStatus { get; set; } = PlacingStatus.Placed;

    public decimal? Score { get; set; }
    public string? ScoreNote { get; set; }

    public decimal? Points { get; set; }
    public PointsSource PointsSource { get; set; } = PointsSource.Computed;

    public int? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
