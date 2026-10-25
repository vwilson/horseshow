using HorseShow.Core.Text;

namespace HorseShow.Core.Tests;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("Jane Smith", "jane smith")]
    [InlineData("  Jane   Smith  ", "jane smith")]
    [InlineData("Smith, Jane", "jane smith")]
    [InlineData("SMITH, JANE", "jane smith")]
    [InlineData("O'Brien, Seán", "sean o brien")]
    [InlineData("Blue Moon (reg. 1234)", "blue moon reg 1234")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalizes_to_canonical_key(string? input, string expected)
    {
        Assert.Equal(expected, NameNormalizer.Normalize(input));
    }

    [Fact]
    public void Two_commas_are_not_treated_as_last_first()
    {
        // "Smith, Jane, Jr." is ambiguous; leave the token order alone.
        Assert.Equal("smith jane jr", NameNormalizer.Normalize("Smith, Jane, Jr."));
    }

    [Fact]
    public void Same_person_written_two_ways_collides()
    {
        Assert.Equal(NameNormalizer.Normalize("Smith, Jane"), NameNormalizer.Normalize("Jane Smith"));
    }
}
