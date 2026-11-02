using HorseShow.Import.Mapping;
using HorseShow.Import.Model;
using HorseShow.Import.Parsing;

namespace HorseShow.Import.Tests;

public class DashListingTests
{
    [Fact]
    public void Dash_separated_listing_splits_into_cells_and_keeps_class_headings()
    {
        var table = TableReaders.FromText(Fixture.Text("dash-listing.txt"));
        Assert.Equal("dashes", table.DelimiterName);

        var doc = RowClassifier.Classify(table);
        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal(("12", "Three-Gaited Show Pleasure Adult"), (doc.Blocks[0].Header!.Number, doc.Blocks[0].Header.Name));
        Assert.Equal(("14", "Five-Gaited Amateur 50 & Over"), (doc.Blocks[1].Header!.Number, doc.Blocks[1].Header.Name));
        Assert.Equal(3, doc.Blocks[0].Rows.Count);
        Assert.Equal(["1.", "Callaway's Blue Norther", "Jane Smith", "Smith Stables"], doc.Blocks[0].Rows[0].Cells);
    }

    [Fact]
    public void Dash_listing_maps_placing_and_three_text_columns()
    {
        var doc = ImportPipeline.ParseText(Fixture.Text("dash-listing.txt"));
        var m = AutoMapper.Propose(doc);

        Assert.Equal(ImportField.Placing, m.FieldFor(0));
        // Headerless text columns default to rider, horse, owner in order; a saved mapping fixes horse-first sources.
        Assert.Equal(ImportField.Rider, m.FieldFor(1));
        Assert.Equal(ImportField.Horse, m.FieldFor(2));
        Assert.Equal(ImportField.Owner, m.FieldFor(3));
    }

    [Fact]
    public void Hyphenated_names_without_spaces_are_not_split()
    {
        var table = TableReaders.FromText("Place\tRider\n1\tMary-Jane Smith-Jones\n2\tBob Jones\n");
        Assert.Equal("tab", table.DelimiterName);
        Assert.Equal("Mary-Jane Smith-Jones", table.Rows[1].Cells[1]);
    }
}
