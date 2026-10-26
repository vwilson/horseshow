using System.Text.Json;
using System.Text.Json.Serialization;
using HorseShow.Import.Model;

namespace HorseShow.Import.Mapping;

/// <summary>Column index to field, with how confident the auto-mapper was so the UI can flag guesses.</summary>
public sealed class ColumnMapping
{
    public Dictionary<int, ImportField> Fields { get; } = [];
    public Dictionary<int, double> Confidence { get; } = [];

    public ImportField FieldFor(int column) => Fields.TryGetValue(column, out var f) ? f : ImportField.Ignore;

    public int? ColumnFor(ImportField field)
        => Fields.Where(kv => kv.Value == field).Select(kv => (int?)kv.Key).FirstOrDefault();

    public bool Has(ImportField field) => Fields.ContainsValue(field);

    public void Set(int column, ImportField field)
    {
        if (field == ImportField.Ignore)
        {
            Fields.Remove(column);
            Confidence.Remove(column);
            return;
        }

        // A field can own one column; reassigning moves it.
        foreach (var other in Fields.Where(kv => kv.Value == field && kv.Key != column).Select(kv => kv.Key).ToList())
        {
            Fields.Remove(other);
            Confidence.Remove(other);
        }

        Fields[column] = field;
        Confidence[column] = 1.0;
    }

    /// <summary>Problems that make the mapping unusable, for the mapping step to show.</summary>
    public IEnumerable<string> Validate(bool hasClassHeaders)
    {
        if (!Has(ImportField.Rider) && !Has(ImportField.Horse) && !Has(ImportField.RiderAndHorse))
        {
            yield return "Map a Rider, Horse, or combined Rider and horse column.";
        }

        if (!Has(ImportField.Placing) && !Has(ImportField.Score) && !Has(ImportField.Points))
        {
            yield return "Map a Placing column (or Score / Points if that is all the sheet has).";
        }

        if (!hasClassHeaders && !Has(ImportField.ClassNumber) && !Has(ImportField.ClassName))
        {
            yield return "No class headers were found and no Class column is mapped; all rows will go into one class.";
        }
    }
}

/// <summary>
/// Serializable form of a mapping, stored on <c>MappingProfile.ColumnMapJson</c>. Keyed by header text when the
/// source had a header row, otherwise by column index, so a saved profile re-applies to the next paste from
/// the same software even when columns move.
/// </summary>
public sealed class MappingProfileData
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    public Dictionary<string, ImportField> ByHeader { get; set; } = [];
    public Dictionary<int, ImportField> ByIndex { get; set; } = [];

    public static MappingProfileData Capture(ColumnMapping mapping, ParsedDocument doc)
    {
        var data = new MappingProfileData();
        foreach (var (col, field) in mapping.Fields)
        {
            var header = doc.HeaderText(col);
            if (!string.IsNullOrWhiteSpace(header))
            {
                data.ByHeader[ColumnSynonyms.Normalize(header)] = field;
            }

            data.ByIndex[col] = field;
        }

        return data;
    }

    /// <summary>Applies to a document. Header matches win; index matches fill in only when the document has no header row.</summary>
    public ColumnMapping Apply(ParsedDocument doc)
    {
        var mapping = new ColumnMapping();
        if (doc.HeaderRow is not null && ByHeader.Count > 0)
        {
            for (var c = 0; c < doc.ColumnCount; c++)
            {
                if (ByHeader.TryGetValue(ColumnSynonyms.Normalize(doc.HeaderText(c)), out var f))
                {
                    mapping.Set(c, f);
                }
            }
        }
        else
        {
            foreach (var (col, field) in ByIndex.Where(kv => kv.Key < doc.ColumnCount))
            {
                mapping.Set(col, field);
            }
        }

        return mapping;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static MappingProfileData FromJson(string json)
        => string.IsNullOrWhiteSpace(json) ? new MappingProfileData() : JsonSerializer.Deserialize<MappingProfileData>(json, Options) ?? new MappingProfileData();
}
