using ClosedXML.Excel;
using HorseShow.Core.Model;
using HorseShow.Core.Scoring;
using HorseShow.Import.Export;

namespace HorseShow.Import.Tests;

public class ExportersTests
{
    private static Exporters.StandingsExport Sample(bool topN = false) => new("2026", "Rider", null, topN,
    [
        new StandingLine { Key = "r1", Name = "Jane Smith", Rank = 1, TotalPoints = 20, CountedPoints = 16, Shows = 2, Classes = 3, Firsts = 1, Seconds = 1,
            PerShow = [new ShowPoints(1, "Spring", new DateOnly(2026, 4, 1), 10, 2, true), new ShowPoints(2, "Summer", new DateOnly(2026, 6, 1), 6, 1, true)] },
        new StandingLine { Key = "r2", Name = "Bob, Jones", Rank = 2, TotalPoints = 6.5m, CountedPoints = 6.5m, Shows = 1, Classes = 1, Eligible = false },
    ]);

    [Fact]
    public void Csv_has_header_and_quotes_commas()
    {
        var csv = Exporters.StandingsCsv(Sample());
        var lines = csv.TrimEnd().Split('\n');

        Assert.Equal("Rank,Rider,Group,Shows,Classes,1st,2nd,3rd,Points,Eligible", lines[0]);
        Assert.Equal("1,Jane Smith,,2,3,1,1,0,16,yes", lines[1]);
        Assert.Equal("2,\"Bob, Jones\",,1,1,0,0,0,6.5,no", lines[2]);
    }

    [Fact]
    public void Csv_adds_total_column_under_top_n()
    {
        var csv = Exporters.StandingsCsv(Sample(topN: true));
        Assert.StartsWith("Rank,Rider,Group,Shows,Classes,1st,2nd,3rd,Total,Points,Eligible", csv);
        Assert.Contains("1,Jane Smith,,2,3,1,1,0,20,16,yes", csv);
    }

    [Fact]
    public void Xlsx_round_trips_with_two_sheets()
    {
        var bytes = Exporters.StandingsXlsx(Sample());

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(["Standings", "Per show"], wb.Worksheets.Select(w => w.Name).ToArray());
        var ws = wb.Worksheet("Standings");
        Assert.Equal("Rank", ws.Cell(3, 1).GetString());
        Assert.Equal("Jane Smith", ws.Cell(4, 2).GetString());
        Assert.Equal(16, ws.Cell(4, 9).GetDouble());
        Assert.Equal(2, wb.Worksheet("Per show").RowsUsed().Count() - 1);
    }

    [Fact]
    public void Show_results_csv_lists_every_result_in_class_order()
    {
        var show = new Show { Name = "S", StartDate = new DateOnly(2026, 4, 1) };
        var a = new ShowClass { Name = "B class", ClassNumber = "2", SortOrder = 20 };
        a.Results.Add(new Result { Placing = 1, Rider = new Rider { Name = "Jane" }, Horse = new Horse { Name = "Blue" }, Points = 10 });
        var b = new ShowClass { Name = "A class", ClassNumber = "1", SortOrder = 10 };
        b.Results.Add(new Result { PlacingStatus = PlacingStatus.Scratched, Rider = new Rider { Name = "Bob" } });
        b.Results.Add(new Result { Placing = 2, Rider = new Rider { Name = "Ann" }, Score = 71.5m });
        show.Classes.Add(a);
        show.Classes.Add(b);

        var lines = Exporters.ShowResultsCsv(show).TrimEnd().Split('\n');

        Assert.Equal(4, lines.Length);
        Assert.StartsWith("1,A class,,2,Placed,,Ann,,71.5,", lines[1]);
        Assert.StartsWith("1,A class,,,Scratched,,Bob,,,", lines[2]);
        Assert.StartsWith("2,B class,,1,Placed,,Jane,Blue,,10", lines[3]);
    }
}
