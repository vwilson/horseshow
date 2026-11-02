using HorseShow.Import.Mapping;
using HorseShow.Import.Model;
using HorseShow.Import.Parsing;

namespace HorseShow.Import;

/// <summary>Convenience entry points for the stages that need no database.</summary>
public static class ImportPipeline
{
    public static ParsedDocument ParseText(string text) => RowClassifier.Classify(TableReaders.FromText(text));

    public static ParsedDocument ParseCsv(Stream stream) => RowClassifier.Classify(TableReaders.FromCsv(stream));

    public static ParsedDocument ParseXlsx(Stream stream, string? sheet = null) => RowClassifier.Classify(TableReaders.FromXlsx(stream, sheet));

    public static ParsedDocument Parse(string fileName, Stream stream)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".xlsx" or ".xlsm" => ParseXlsx(stream),
            ".csv" or ".tsv" or ".txt" => ParseCsv(stream),
            _ => throw new NotSupportedException($"Unsupported file type: {Path.GetExtension(fileName)}. Use .csv, .tsv, .txt or .xlsx."),
        };

    public static ColumnMapping ProposeMapping(ParsedDocument doc) => AutoMapper.Propose(doc);
}
