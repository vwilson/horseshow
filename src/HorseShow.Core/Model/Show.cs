namespace HorseShow.Core.Model;

public class Show
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season? Season { get; set; }
    public required string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Venue { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set when an import created this show, so undoing that import can remove it if still empty.</summary>
    public int? CreatedByImportBatchId { get; set; }

    public ICollection<ShowClass> Classes { get; set; } = [];
}
