using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using HorseShow.Import.Model;

namespace HorseShow.Import.Parsing;

/// <summary>Turns pasted text, CSV, or XLSX into a <see cref="RawTable"/>. No interpretation happens here.</summary>
public static partial class TableReaders
{
    [GeneratedRegex(@"\s{2,}|\t")]
    private static partial Regex MultiSpace();

    /// <summary>
    /// Pasted text. Picks the delimiter that gives the most consistent column count across lines:
    /// tab, comma, pipe, semicolon, then runs of two or more spaces (PDF copies, fixed-width listings).
    /// Lines that do not split stay as single-cell rows so the classifier can treat them as section headers.
    /// </summary>
    public static RawTable FromText(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var (delimiter, name) = DelimiterDetector.Detect(lines);

        var rows = new List<RawRow>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var cells = delimiter switch
            {
                null => [line.Trim()],
                ' ' => MultiSpace().Split(line.Trim()),
                _ => SplitDelimited(line, delimiter.Value),
            };
            rows.Add(new RawRow(i + 1, cells.Select(c => c.Trim()).ToArray()));
        }

        return new RawTable { Rows = rows, DelimiterName = name };
    }

    public static RawTable FromCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            DetectDelimiter = true,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
        };
        using var parser = new CsvParser(reader, config);
        var rows = new List<RawRow>();
        while (parser.Read())
        {
            if (parser.Record is { } record && record.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                rows.Add(new RawRow(parser.Row, record));
            }
        }

        return new RawTable { Rows = rows, DelimiterName = DelimiterDetector.NameOf(parser.Delimiter) };
    }

    public static RawTable FromXlsx(Stream stream, string? sheetName = null)
    {
        using var workbook = new XLWorkbook(stream);
        var names = workbook.Worksheets.Select(w => w.Name).ToList();
        var sheet = sheetName is null ? workbook.Worksheets.First() : workbook.Worksheet(sheetName);
        var rows = new List<RawRow>();
        var used = sheet.RangeUsed();
        if (used is not null)
        {
            var firstCol = used.FirstColumn().ColumnNumber();
            var lastCol = used.LastColumn().ColumnNumber();
            foreach (var row in used.Rows())
            {
                var cells = new string[lastCol - firstCol + 1];
                for (var c = firstCol; c <= lastCol; c++)
                {
                    cells[c - firstCol] = CellText(row.Cell(c - firstCol + 1));
                }

                if (cells.Any(x => x.Length > 0))
                {
                    rows.Add(new RawRow(row.RowNumber(), cells));
                }
            }
        }

        return new RawTable { Rows = rows, SheetName = sheet.Name, SheetNames = names, DelimiterName = "xlsx" };
    }

    private static string CellText(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        var value = cell.Value;
        if (value.IsNumber)
        {
            var d = value.GetNumber();
            return d == Math.Floor(d) && Math.Abs(d) < 1e15 ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture);
        }

        return value.IsDateTime ? value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : cell.GetFormattedString().Trim();
    }

    private static string[] SplitDelimited(string line, char delimiter)
    {
        // CsvHelper handles quoted cells that contain the delimiter.
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Delimiter = delimiter.ToString(),
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
        };
        using var parser = new CsvParser(new StringReader(line), config);
        return parser.Read() ? parser.Record ?? [line] : [line];
    }
}

public static class DelimiterDetector
{
    private static readonly (char Delimiter, string Name)[] Candidates =
    [
        ('\t', "tab"), (',', "comma"), ('|', "pipe"), (';', "semicolon"),
    ];

    public static string NameOf(string delimiter) => delimiter switch
    {
        "\t" => "tab", "," => "comma", "|" => "pipe", ";" => "semicolon", _ => delimiter,
    };

    /// <summary>Returns the delimiter and its display name; ' ' means runs of two or more spaces; null means one column.</summary>
    public static (char? Delimiter, string Name) Detect(IReadOnlyList<string> lines)
    {
        var nonEmpty = lines.Where(l => l.Trim().Length > 0).ToList();
        if (nonEmpty.Count == 0)
        {
            return (null, "none");
        }

        var best = (Delimiter: (char?)null, Name: "none", Score: 0.0);
        foreach (var (delimiter, name) in Candidates)
        {
            var score = Score(nonEmpty, l => l.Count(ch => ch == delimiter) + 1);
            if (score > best.Score)
            {
                best = (delimiter, name, score);
            }
        }

        var spaceScore = Score(nonEmpty, l => Regex.Split(l.Trim(), @"\s{2,}").Length);
        if (spaceScore > best.Score)
        {
            best = (' ', "spaces", spaceScore);
        }

        return best.Score > 0 ? (best.Delimiter, best.Name) : (null, "none");
    }

    /// <summary>
    /// Share of lines that split into two or more cells, weighted by how consistent the cell count is.
    /// Section header lines (one cell) are expected and do not count against a delimiter.
    /// </summary>
    private static double Score(List<string> lines, Func<string, int> cellCount)
    {
        var counts = lines.Select(cellCount).Where(c => c >= 2).ToList();
        if (counts.Count == 0)
        {
            return 0;
        }

        var coverage = (double)counts.Count / lines.Count;
        var mode = counts.GroupBy(c => c).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First();
        var consistency = (double)mode.Count() / counts.Count;
        return coverage * (0.5 + 0.5 * consistency);
    }
}
