using HorseShow.Import;
using HorseShow.Import.Model;
using HorseShow.Import.Parsing;

namespace HorseShow.Import.Tests;

public class TableReaderTests
{
    [Fact]
    public void Excel_paste_is_tab_delimited_with_header()
    {
        var table = TableReaders.FromText(Fixture.Text("excel-paste-single-class.tsv"));

        Assert.Equal("tab", table.DelimiterName);
        Assert.Equal(7, table.Rows.Count);
        Assert.Equal(5, table.ColumnCount);
        Assert.Equal("Smith, Jane", table.Rows[1].Cells[3]);
    }

    [Fact]
    public void Section_lines_do_not_confuse_delimiter_detection()
    {
        var table = TableReaders.FromText(Fixture.Text("multi-class-sections.txt"));

        Assert.Equal("tab", table.DelimiterName);
        Assert.Contains(table.Rows, r => r.Cells.Length == 1 && r.Cells[0].StartsWith("Class 12"));
        Assert.Contains(table.Rows, r => r.Cells.Length == 4 && r.Cells[2] == "Blue Moon");
    }

    [Fact]
    public void Runs_of_spaces_split_fixed_width_listings()
    {
        var table = TableReaders.FromText(Fixture.Text("space-separated-pdf.txt"));

        Assert.Equal("spaces", table.DelimiterName);
        var row = table.Rows.First(r => r.Cells.Length == 4);
        Assert.Equal(["1", "204", "Blue Moon", "Jane Smith"], row.Cells);
    }

    [Fact]
    public void Csv_handles_quoted_names_with_commas()
    {
        using var stream = Fixture.Open("with-header-classes.csv");
        var table = TableReaders.FromCsv(stream);

        Assert.Equal("comma", table.DelimiterName);
        Assert.Equal(7, table.Rows.Count);
        Assert.Equal("Smith, Jane", table.Rows[1].Cells[3]);
        Assert.Equal(string.Empty, table.Rows[4].Cells[5]);
    }

    [Fact]
    public void Comma_paste_with_quotes_also_works_through_text_reader()
    {
        var table = TableReaders.FromText(Fixture.Text("wide-score-sheet.csv"));

        Assert.Equal("comma", table.DelimiterName);
        Assert.Equal("Smith, Jane", table.Rows[1].Cells[0]);
    }

    [Fact]
    public void Xlsx_round_trips_numbers_as_plain_text()
    {
        using var ms = new MemoryStream();
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.AddWorksheet("Results");
            ws.Cell(1, 1).Value = "Place"; ws.Cell(1, 2).Value = "Rider"; ws.Cell(1, 3).Value = "Horse"; ws.Cell(1, 4).Value = "Score";
            ws.Cell(2, 1).Value = 1; ws.Cell(2, 2).Value = "Jane Smith"; ws.Cell(2, 3).Value = "Blue Moon"; ws.Cell(2, 4).Value = 84.5;
            ws.Cell(3, 1).Value = 2; ws.Cell(3, 2).Value = "Bob Jones"; ws.Cell(3, 3).Value = "Red Sky"; ws.Cell(3, 4).Value = 81;
            wb.AddWorksheet("Notes").Cell(1, 1).Value = "ignore me";
            wb.SaveAs(ms);
        }

        ms.Position = 0;
        var table = TableReaders.FromXlsx(ms);

        Assert.Equal("Results", table.SheetName);
        Assert.Equal(["Results", "Notes"], table.SheetNames);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(["1", "Jane Smith", "Blue Moon", "84.5"], table.Rows[1].Cells);
        Assert.Equal("81", table.Rows[2].Cells[3]);
    }
}

public class ClassHeaderParserTests
{
    [Theory]
    [InlineData("Class 12 - Hunter Under Saddle (8 entries)", "12", "Hunter Under Saddle", 8)]
    [InlineData("Class 12: Hunter Under Saddle", "12", "Hunter Under Saddle", null)]
    [InlineData("#15 Walk-Trot Pleasure 11 & Under", "15", "Walk-Trot Pleasure 11 & Under", null)]
    [InlineData("12. Hunter Under Saddle", "12", "Hunter Under Saddle", null)]
    [InlineData("Hunter Under Saddle (8)", null, "Hunter Under Saddle", 8)]
    [InlineData("Equitation on the Flat - 5 entries", null, "Equitation on the Flat", 5)]
    [InlineData("Class 7", "7", null, null)]
    [InlineData("CLASS 101A OPEN HUNTER", "101A", "OPEN HUNTER", null)]
    public void Recognises_class_header_shapes(string line, string? number, string? name, int? entries)
    {
        var h = ClassHeaderParser.Parse(line, 1);

        Assert.NotNull(h);
        Assert.Equal(number, h.Number);
        Assert.Equal(name, h.Name);
        Assert.Equal(entries, h.EntryCount);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("204")]
    [InlineData("Jane Smith")]
    [InlineData("")]
    [InlineData("1st")]
    public void Rejects_things_that_are_not_headers(string line)
    {
        Assert.Null(ClassHeaderParser.Parse(line, 1));
    }

    [Fact]
    public void Entries_only_line_is_recognised()
    {
        Assert.Equal(3, ClassHeaderParser.ParseEntriesOnly("Entries: 3"));
        Assert.Null(ClassHeaderParser.ParseEntriesOnly("Entries"));
    }
}

public class RowClassifierTests
{
    [Fact]
    public void Multi_class_paste_yields_three_blocks_with_headers_and_drops_noise()
    {
        var doc = ImportPipeline.ParseText(Fixture.Text("multi-class-sections.txt"));

        Assert.NotNull(doc.HeaderRow);
        Assert.Equal(3, doc.Blocks.Count);
        Assert.Equal(("12", "Hunter Under Saddle", 8), (doc.Blocks[0].Header!.Number, doc.Blocks[0].Header.Name, doc.Blocks[0].Header.EntryCount));
        Assert.Equal(4, doc.Blocks[0].Rows.Count);
        Assert.Equal("14", doc.Blocks[1].Header!.Number);
        Assert.Equal(3, doc.Blocks[1].Rows.Count);
        Assert.Equal(("15", 3), (doc.Blocks[2].Header!.Number, doc.Blocks[2].Header.EntryCount)); // "Entries: 3" folded in
        Assert.Equal(3, doc.Blocks[2].Rows.Count);
        Assert.Equal(10, doc.DataRowCount);
        Assert.DoesNotContain(doc.Blocks.SelectMany(b => b.Rows), r => r.Joined.StartsWith("Judge") || r.Joined.StartsWith("Page"));
        // the repeated "Place Back # Horse Rider" rows are noise, not data
        Assert.DoesNotContain(doc.Blocks.SelectMany(b => b.Rows), r => r.Cells[0] == "Place");
    }

    [Fact]
    public void Single_class_paste_has_one_headerless_block()
    {
        var doc = ImportPipeline.ParseText(Fixture.Text("excel-paste-single-class.tsv"));

        Assert.NotNull(doc.HeaderRow);
        var block = Assert.Single(doc.Blocks);
        Assert.Null(block.Header);
        Assert.Equal(6, block.Rows.Count);
    }

    [Fact]
    public void Space_separated_listing_without_header_row_still_finds_sections()
    {
        var doc = ImportPipeline.ParseText(Fixture.Text("space-separated-pdf.txt"));

        Assert.Null(doc.HeaderRow);
        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("Hunter Under Saddle", doc.Blocks[0].Header!.Name);
        Assert.Equal(8, doc.Blocks[0].Header.EntryCount);
        Assert.Equal(3, doc.Blocks[0].Rows.Count);
    }
}
