using System.Text.RegularExpressions;
using HorseShow.Import.Model;

namespace HorseShow.Import.Mapping;

/// <summary>
/// Header text to field. Keys are normalized: lower case, letters and digits only.
/// Add a synonym here when a new source uses a header the auto-mapper misses.
/// </summary>
public static partial class ColumnSynonyms
{
    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonAlnum();

    private static readonly Dictionary<string, ImportField> Map = Build();

    public static string Normalize(string? header) => NonAlnum().Replace((header ?? string.Empty).ToLowerInvariant(), string.Empty);

    public static ImportField? Lookup(string? header)
        => Map.TryGetValue(Normalize(header), out var f) ? f : null;

    /// <summary>How many cells of a row read as known column headers.</summary>
    public static int HeaderScore(IEnumerable<string> cells) => cells.Count(c => Lookup(c) is not null);

    private static Dictionary<string, ImportField> Build()
    {
        var map = new Dictionary<string, ImportField>();
        void Add(ImportField field, params string[] names)
        {
            foreach (var n in names)
            {
                map[Normalize(n)] = field;
            }
        }

        Add(ImportField.Placing, "place", "placing", "placed", "pl", "plc", "pos", "position", "ribbon", "rank", "finish", "result", "award", "standing");
        Add(ImportField.BackNumber, "back", "back #", "back no", "back number", "number", "no", "no.", "num", "#", "entry", "entry #", "entry no", "entry number",
            "bridle", "bridle #", "bridle no", "bib", "exhibitor #", "exhibitor no", "rider #", "horse #", "id");
        Add(ImportField.Rider, "rider", "rider name", "exhibitor", "exhibitor name", "handler", "competitor", "shown by", "driver", "jockey", "athlete", "member", "name", "student", "participant", "person");
        Add(ImportField.Horse, "horse", "horse name", "mount", "pony", "animal", "equine", "horse/pony", "horse or pony");
        Add(ImportField.RiderAndHorse, "rider/horse", "rider / horse", "horse/rider", "horse / rider", "rider and horse", "horse and rider", "combination", "pair", "entry name", "rider on horse");
        Add(ImportField.Owner, "owner", "owned by", "owner name");
        Add(ImportField.Score, "score", "scores", "pct", "percent", "percentage", "%", "total score", "judge score", "final score", "marks");
        Add(ImportField.Points, "points", "pts", "point", "season points", "circuit points", "year end points", "yep", "hp points", "high point");
        Add(ImportField.ClassNumber, "class", "class #", "class no", "class number", "cls", "cls #", "event", "event #", "event no", "class id");
        Add(ImportField.ClassName, "class name", "class title", "classname", "description", "event name", "title", "division class");
        Add(ImportField.EntryCount, "entries", "shown", "# entries", "no entries", "num entries", "entry count", "in class", "starters", "competed", "entered");
        return map;
    }
}
