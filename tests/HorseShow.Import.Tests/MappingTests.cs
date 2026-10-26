using HorseShow.Core.Model;
using HorseShow.Import;
using HorseShow.Import.Mapping;
using HorseShow.Import.Model;
using HorseShow.Import.Normalization;

namespace HorseShow.Import.Tests;

public class AutoMapperTests
{
    [Fact]
    public void Header_synonyms_map_every_column()
    {
        var doc = ImportPipeline.ParseText(Fixture.Text("excel-paste-single-class.tsv"));
        var m = AutoMapper.Propose(doc);

        Assert.Equal(ImportField.Placing, m.FieldFor(0));
        Assert.Equal(ImportField.BackNumber, m.FieldFor(1));
        Assert.Equal(ImportField.Horse, m.FieldFor(2));
        Assert.Equal(ImportField.Rider, m.FieldFor(3));
        Assert.Equal(ImportField.Owner, m.FieldFor(4));
        Assert.Empty(m.Validate(hasClassHeaders: false).Where(v => !v.Contains("one class")));
    }

    [Fact]
    public void Csv_with_class_columns_maps_class_number_name_score_and_points()
    {
        using var s = Fixture.Open("with-header-classes.csv");
        var m = AutoMapper.Propose(ImportPipeline.ParseCsv(s));

        Assert.Equal(ImportField.ClassNumber, m.FieldFor(0));
        Assert.Equal(ImportField.ClassName, m.FieldFor(1));
        Assert.Equal(ImportField.Placing, m.FieldFor(2));
        Assert.Equal(ImportField.Rider, m.FieldFor(3));
        Assert.Equal(ImportField.Horse, m.FieldFor(4));
        Assert.Equal(ImportField.Score, m.FieldFor(5));
        Assert.Equal(ImportField.Points, m.FieldFor(6));
    }

    [Fact]
    public void Without_a_header_values_decide()
    {
        var doc = ImportPipeline.ParseText(Fixture.Text("space-separated-pdf.txt"));
        var m = AutoMapper.Propose(doc);

        Assert.Equal(ImportField.Placing, m.FieldFor(0));
        Assert.Equal(ImportField.BackNumber, m.FieldFor(1));
        Assert.Equal(ImportField.Rider, m.FieldFor(2)); // first text column; the user flips it in the grid
        Assert.Equal(ImportField.Horse, m.FieldFor(3));
        Assert.True(m.Confidence[2] < m.Confidence[0]);
    }

    [Fact]
    public void Combined_rider_horse_column_is_detected()
    {
        var doc = ImportPipeline.ParseText(Fixture.Text("combined-rider-horse.txt"));
        var m = AutoMapper.Propose(doc);

        Assert.Equal(ImportField.Placing, m.FieldFor(0));
        Assert.Equal(ImportField.RiderAndHorse, m.FieldFor(1));
    }

    [Fact]
    public void Set_moves_a_field_between_columns()
    {
        var m = new ColumnMapping();
        m.Set(0, ImportField.Rider);
        m.Set(1, ImportField.Rider);

        Assert.Equal(ImportField.Ignore, m.FieldFor(0));
        Assert.Equal(ImportField.Rider, m.FieldFor(1));
    }

    [Fact]
    public void Profile_round_trips_by_header_and_falls_back_to_index()
    {
        var doc = ImportPipeline.ParseText(Fixture.Text("excel-paste-single-class.tsv"));
        var original = AutoMapper.Propose(doc);
        original.Set(4, ImportField.Ignore);
        var json = MappingProfileData.Capture(original, doc).ToJson();

        var reloaded = MappingProfileData.FromJson(json);
        var applied = reloaded.Apply(doc);
        Assert.Equal(original.Fields, applied.Fields);

        // Same columns, shuffled: header names win.
        var shuffled = ImportPipeline.ParseText("Rider\tPlace\tHorse\nJane\t1\tBlue\n");
        var m2 = reloaded.Apply(shuffled);
        Assert.Equal(ImportField.Rider, m2.FieldFor(0));
        Assert.Equal(ImportField.Placing, m2.FieldFor(1));
        Assert.Equal(ImportField.Horse, m2.FieldFor(2));

        // No header row: indices apply.
        var headerless = ImportPipeline.ParseText("1\t204\tBlue Moon\tJane Smith\n2\t117\tRed Sky\tBob Jones\n");
        var m3 = reloaded.Apply(headerless);
        Assert.Equal(ImportField.Placing, m3.FieldFor(0));
        Assert.Equal(ImportField.Rider, m3.FieldFor(3));
    }
}

public class NormalizationTests
{
    [Theory]
    [InlineData("1", 1, PlacingStatus.Placed)]
    [InlineData("1st", 1, PlacingStatus.Placed)]
    [InlineData("2.", 2, PlacingStatus.Placed)]
    [InlineData("Third", 3, PlacingStatus.Placed)]
    [InlineData("Champion", 1, PlacingStatus.Placed)]
    [InlineData("Res", 2, PlacingStatus.Placed)]
    [InlineData("DQ", null, PlacingStatus.Disqualified)]
    [InlineData("scr", null, PlacingStatus.Scratched)]
    [InlineData("EL", null, PlacingStatus.Eliminated)]
    [InlineData("NP", null, PlacingStatus.NoPlace)]
    [InlineData("0", null, PlacingStatus.NoPlace)]
    public void Parses_placings_and_status_tokens(string text, int? placing, PlacingStatus status)
    {
        var p = PlacingParser.Parse(text);
        Assert.NotNull(p);
        Assert.Equal((placing, status), p.Value);
    }

    [Theory]
    [InlineData("Jane Smith")]
    [InlineData("")]
    [InlineData("1000")]
    public void Rejects_non_placings(string text) => Assert.Null(PlacingParser.Parse(text));

    [Theory]
    [InlineData("68.750%", "68.750")]
    [InlineData("84.5", "84.5")]
    [InlineData("81", "81")]
    [InlineData("72,5", "72.5")]
    [InlineData("n/a", null)]
    public void Parses_scores(string text, string? expected)
        => Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), ScoreParser.Parse(text));

    [Theory]
    [InlineData("Jane Smith on Blue Moon", "Jane Smith", "Blue Moon")]
    [InlineData("Bob Jones / Red Sky", "Bob Jones", "Red Sky")]
    [InlineData("Grey Ghost (Ann Lee)", "Ann Lee", "Grey Ghost")]
    [InlineData("Lily Chen riding Midnight", "Lily Chen", "Midnight")]
    public void Splits_combined_names(string text, string rider, string horse)
        => Assert.Equal((rider, horse), RiderHorseSplitter.Split(text));

    [Fact]
    public void Single_name_does_not_split() => Assert.Null(RiderHorseSplitter.Split("Jane Smith"));
}
