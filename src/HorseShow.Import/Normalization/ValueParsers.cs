using System.Globalization;
using System.Text.RegularExpressions;

namespace HorseShow.Import.Normalization;

public static partial class ScoreParser
{
    [GeneratedRegex(@"-?\d+(?:[.,]\d+)?")]
    private static partial Regex Number();

    /// <summary>First number in the cell; "65.432%", "72.5", "81" all parse. Null when there is none.</summary>
    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var m = Number().Match(text);
        if (!m.Success)
        {
            return null;
        }

        var s = m.Value.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}

/// <summary>Splits "Jane Smith on Blue Moon", "Jane Smith / Blue Moon", "Blue Moon (Jane Smith)".</summary>
public static partial class RiderHorseSplitter
{
    [GeneratedRegex(@"^(?<a>.+?)\s+(?:on|riding|aboard|rode|rides)\s+(?<b>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex OnPattern();

    [GeneratedRegex(@"^(?<a>.+?)\s*[/|]\s*(?<b>.+)$")]
    private static partial Regex SlashPattern();

    [GeneratedRegex(@"^(?<a>.+?)\s*\((?<b>[^)]+)\)\s*$")]
    private static partial Regex ParenPattern();

    /// <returns>Rider and horse, or null when the text has only one name.</returns>
    public static (string Rider, string Horse)? Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var s = text.Trim();
        var m = OnPattern().Match(s);
        if (m.Success)
        {
            return (m.Groups["a"].Value.Trim(), m.Groups["b"].Value.Trim());
        }

        m = SlashPattern().Match(s);
        if (m.Success)
        {
            return (m.Groups["a"].Value.Trim(), m.Groups["b"].Value.Trim());
        }

        // "Horse (Rider)" is the common parenthesised order on hunter sheets.
        m = ParenPattern().Match(s);
        if (m.Success)
        {
            return (m.Groups["b"].Value.Trim(), m.Groups["a"].Value.Trim());
        }

        return null;
    }
}

public static class EntryCountParser
{
    public static int? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return digits.Length > 0 && int.TryParse(digits, out var n) ? n : null;
    }
}
