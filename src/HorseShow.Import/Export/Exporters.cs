using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using HorseShow.Core.Model;
using HorseShow.Core.Scoring;

namespace HorseShow.Import.Export;

/// <summary>Standings and show results to CSV or XLSX. Lives next to the import readers so the two formats stay symmetric.</summary>
public static class Exporters
{
    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    public sealed record StandingsExport(string SeasonName, string Basis, string? DivisionName, bool TopN, IReadOnlyList<StandingLine> Lines);

    public static string StandingsCsv(StandingsExport export)
    {
        using var sw = new StringWriter();
        using var csv = new CsvWriter(sw, Config);
        WriteStandingsHeader(csv, export);
        foreach (var l in export.Lines)
        {
            WriteStandingsRow(csv, export, l);
        }

        return sw.ToString();
    }

    public static byte[] StandingsXlsx(StandingsExport export)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(Sanitize(export.DivisionName ?? "Standings"));
        var title = $"{export.SeasonName} standings by {export.Basis}{(export.DivisionName is null ? "" : $" · {export.DivisionName}")}";
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        var headers = StandingsColumns(export).ToList();
        for (var c = 0; c < headers.Count; c++)
        {
            ws.Cell(3, c + 1).Value = headers[c];
            ws.Cell(3, c + 1).Style.Font.Bold = true;
            ws.Cell(3, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EFEB");
        }

        var r = 4;
        foreach (var l in export.Lines)
        {
            var values = StandingsValues(export, l).ToList();
            for (var c = 0; c < values.Count; c++)
            {
                var cell = ws.Cell(r, c + 1);
                switch (values[c])
                {
                    case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = "0.###"; break;
                    case int i: cell.Value = i; break;
                    case bool b: cell.Value = b ? "yes" : "no"; break;
                    default: cell.Value = values[c]?.ToString() ?? string.Empty; break;
                }
            }

            if (!l.Eligible)
            {
                ws.Row(r).Style.Font.FontColor = XLColor.Gray;
            }

            r++;
        }

        // Per-show breakdown on a second sheet for the curious.
        var detail = wb.AddWorksheet("Per show");
        detail.Cell(1, 1).Value = "Competitor"; detail.Cell(1, 2).Value = "Date"; detail.Cell(1, 3).Value = "Show";
        detail.Cell(1, 4).Value = "Classes"; detail.Cell(1, 5).Value = "Points"; detail.Cell(1, 6).Value = "Counted";
        detail.Row(1).Style.Font.Bold = true;
        var dr = 2;
        foreach (var l in export.Lines)
        {
            foreach (var s in l.PerShow)
            {
                detail.Cell(dr, 1).Value = l.SecondaryName is null ? l.Name : $"{l.Name} / {l.SecondaryName}";
                detail.Cell(dr, 2).Value = s.Date.ToDateTime(TimeOnly.MinValue);
                detail.Cell(dr, 2).Style.DateFormat.Format = "yyyy-mm-dd";
                detail.Cell(dr, 3).Value = s.ShowName;
                detail.Cell(dr, 4).Value = s.Classes;
                detail.Cell(dr, 5).Value = s.Points;
                detail.Cell(dr, 6).Value = s.Counted ? "yes" : "no";
                dr++;
            }
        }

        ws.Columns().AdjustToContents();
        detail.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static string ShowResultsCsv(Show show)
    {
        using var sw = new StringWriter();
        using var csv = new CsvWriter(sw, Config);
        foreach (var h in new[] { "Class #", "Class", "Division", "Place", "Status", "Back #", "Rider", "Horse", "Score", "Points" })
        {
            csv.WriteField(h);
        }

        csv.NextRecord();
        foreach (var cls in show.Classes.OrderBy(c => c.SortOrder))
        {
            foreach (var r in cls.Results.OrderBy(x => x.PlacingStatus == PlacingStatus.Placed ? 0 : 1).ThenBy(x => x.Placing ?? int.MaxValue))
            {
                csv.WriteField(cls.ClassNumber);
                csv.WriteField(cls.Name);
                csv.WriteField(cls.Division?.Name);
                csv.WriteField(r.Placing);
                csv.WriteField(r.PlacingStatus.ToString());
                csv.WriteField(r.BackNumber);
                csv.WriteField(r.Rider?.DisplayName ?? r.Rider?.Name);
                csv.WriteField(r.Horse?.Name);
                csv.WriteField(r.Score);
                csv.WriteField(r.Points);
                csv.NextRecord();
            }
        }

        return sw.ToString();
    }

    private static IEnumerable<string> StandingsColumns(StandingsExport e)
    {
        yield return "Rank";
        yield return e.Basis == "Horse" ? "Horse" : "Rider";
        if (e.Basis == "Combination") yield return "Horse";
        if (e.Basis != "Horse") yield return "Group";
        yield return "Shows";
        yield return "Classes";
        yield return "1st";
        yield return "2nd";
        yield return "3rd";
        if (e.TopN) yield return "Total";
        yield return "Points";
        yield return "Eligible";
    }

    private static IEnumerable<object?> StandingsValues(StandingsExport e, StandingLine l)
    {
        yield return l.Rank;
        yield return l.Name;
        if (e.Basis == "Combination") yield return l.SecondaryName;
        if (e.Basis != "Horse") yield return l.RiderGroup;
        yield return l.Shows;
        yield return l.Classes;
        yield return l.Firsts;
        yield return l.Seconds;
        yield return l.Thirds;
        if (e.TopN) yield return l.TotalPoints;
        yield return l.CountedPoints;
        yield return l.Eligible;
    }

    private static void WriteStandingsHeader(CsvWriter csv, StandingsExport e)
    {
        foreach (var h in StandingsColumns(e)) csv.WriteField(h);
        csv.NextRecord();
    }

    private static void WriteStandingsRow(CsvWriter csv, StandingsExport e, StandingLine l)
    {
        foreach (var v in StandingsValues(e, l))
        {
            csv.WriteField(v is bool b ? (b ? "yes" : "no") : v);
        }

        csv.NextRecord();
    }

    private static string Sanitize(string name)
    {
        var bad = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var s = new string(name.Select(c => bad.Contains(c) ? ' ' : c).ToArray()).Trim();
        return s.Length == 0 ? "Sheet" : s[..Math.Min(31, s.Length)];
    }
}
