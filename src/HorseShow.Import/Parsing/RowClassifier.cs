using HorseShow.Import.Mapping;
using HorseShow.Import.Model;
using HorseShow.Import.Normalization;

namespace HorseShow.Import.Parsing;

/// <summary>
/// Sorts raw rows into header, section (class), data and noise, and groups data rows under
/// the class header that precedes them. Real pastes carry repeated headers, page footers,
/// judge lines and blank separators; those become noise.
/// </summary>
public static class RowClassifier
{
    private static readonly string[] NoisePrefixes = ["judge", "judges", "page ", "printed", "report", "generated", "total", "results", "placings"];

    public static ParsedDocument Classify(RawTable table)
    {
        var columnCount = table.ColumnCount;
        var classification = new List<(int, RowKind)>();
        var blocks = new List<ClassBlock>();
        RawRow? headerRow = null;
        ClassBlock? current = null;

        foreach (var row in table.Rows)
        {
            var kind = Decide(row, headerRow is not null, columnCount);
            classification.Add((row.LineNumber, kind));

            switch (kind)
            {
                case RowKind.Header:
                    headerRow = row;
                    break;
                case RowKind.Section:
                    var header = ClassHeaderParser.Parse(row.Joined, row.LineNumber);
                    if (header is null && ClassHeaderParser.ParseEntriesOnly(row.Joined) is int entries && current?.Header is not null)
                    {
                        // "Entries: 8" line under a class header: fold into the current block.
                        var h = current.Header with { EntryCount = entries };
                        var replaced = new ClassBlock { Header = h };
                        replaced.Rows.AddRange(current.Rows);
                        blocks[^1] = replaced;
                        current = replaced;
                        break;
                    }

                    current = new ClassBlock { Header = header ?? new ClassHeader(null, row.Joined, null, row.LineNumber) };
                    blocks.Add(current);
                    break;
                case RowKind.Data:
                    if (current is null)
                    {
                        current = new ClassBlock();
                        blocks.Add(current);
                    }

                    current.Rows.Add(row);
                    break;
            }
        }

        var doc = new ParsedDocument { Table = table, HeaderRow = headerRow, ColumnCount = columnCount, Classification = classification };
        // A header with no rows under it (a report title, a class with nothing recorded) carries no results.
        doc.Blocks.AddRange(blocks.Where(b => b.Rows.Count > 0));
        return doc;
    }

    private static RowKind Decide(RawRow row, bool haveHeader, int columnCount)
    {
        var nonEmpty = row.NonEmptyCount;
        if (nonEmpty == 0)
        {
            return RowKind.Noise;
        }

        var joined = row.Joined;
        var lower = joined.ToLowerInvariant();

        if (nonEmpty >= 2 && ColumnSynonyms.HeaderScore(row.Cells) >= 2 && !row.Cells.Any(PlacingParser.LooksLikePlacing))
        {
            return haveHeader ? RowKind.Noise : RowKind.Header;
        }

        if (nonEmpty == 1)
        {
            if (ClassHeaderParser.ParseEntriesOnly(joined) is not null)
            {
                return RowKind.Section;
            }

            if (NoisePrefixes.Any(p => lower.StartsWith(p)))
            {
                return RowKind.Noise;
            }

            return ClassHeaderParser.Parse(joined, row.LineNumber) is not null ? RowKind.Section : RowKind.Noise;
        }

        // "Class 12" | "Hunter Under Saddle" split across two cells of a wider table. Anything else with
        // two or more cells is data; a 2-cell data row like "1 | Jane Smith" must never become a class.
        if (nonEmpty == 2 && columnCount > 2 && ClassHeaderParser.IsExplicit(joined))
        {
            return RowKind.Section;
        }

        return RowKind.Data;
    }
}
