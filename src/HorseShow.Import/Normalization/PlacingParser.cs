using System.Text.RegularExpressions;
using HorseShow.Core.Model;

namespace HorseShow.Import.Normalization;

/// <summary>"1", "1st", "1.", "First", "DQ", "SCR" and friends to a placing or status.</summary>
public static partial class PlacingParser
{
    [GeneratedRegex(@"^\s*(?<n>\d{1,3})\s*(?:st|nd|rd|th|\.|\))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Numeric();

    private static readonly Dictionary<string, int> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["first"] = 1, ["second"] = 2, ["third"] = 3, ["fourth"] = 4, ["fifth"] = 5, ["sixth"] = 6,
        ["seventh"] = 7, ["eighth"] = 8, ["ninth"] = 9, ["tenth"] = 10, ["eleventh"] = 11, ["twelfth"] = 12,
        ["champion"] = 1, ["ch"] = 1, ["reserve"] = 2, ["res"] = 2, ["rc"] = 2, ["reserve champion"] = 2,
    };

    private static readonly Dictionary<string, PlacingStatus> StatusTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dq"] = PlacingStatus.Disqualified, ["dsq"] = PlacingStatus.Disqualified, ["disq"] = PlacingStatus.Disqualified, ["disqualified"] = PlacingStatus.Disqualified,
        ["scr"] = PlacingStatus.Scratched, ["scratch"] = PlacingStatus.Scratched, ["scratched"] = PlacingStatus.Scratched, ["wd"] = PlacingStatus.Scratched,
        ["w/d"] = PlacingStatus.Scratched, ["withdrawn"] = PlacingStatus.Scratched, ["ns"] = PlacingStatus.Scratched, ["no show"] = PlacingStatus.Scratched, ["absent"] = PlacingStatus.Scratched,
        ["el"] = PlacingStatus.Eliminated, ["elim"] = PlacingStatus.Eliminated, ["eliminated"] = PlacingStatus.Eliminated, ["e"] = PlacingStatus.Eliminated,
        ["ret"] = PlacingStatus.Eliminated, ["retired"] = PlacingStatus.Eliminated, ["rt"] = PlacingStatus.Eliminated, ["fall"] = PlacingStatus.Eliminated, ["off course"] = PlacingStatus.Eliminated,
        ["np"] = PlacingStatus.NoPlace, ["no place"] = PlacingStatus.NoPlace, ["unplaced"] = PlacingStatus.NoPlace, ["-"] = PlacingStatus.NoPlace, ["--"] = PlacingStatus.NoPlace,
        ["nq"] = PlacingStatus.NoPlace, ["part"] = PlacingStatus.NoPlace, ["participant"] = PlacingStatus.NoPlace, ["x"] = PlacingStatus.NoPlace,
    };

    public static (int? Placing, PlacingStatus Status)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var s = text.Trim();
        var m = Numeric().Match(s);
        if (m.Success)
        {
            var n = int.Parse(m.Groups["n"].Value);
            return n == 0 ? (null, PlacingStatus.NoPlace) : (n, PlacingStatus.Placed);
        }

        if (Words.TryGetValue(s, out var w))
        {
            return (w, PlacingStatus.Placed);
        }

        if (ParseStatusToken(s) is { } status)
        {
            return (null, status);
        }

        return null;
    }

    public static PlacingStatus? ParseStatusToken(string? text)
        => text is not null && StatusTokens.TryGetValue(text.Trim(), out var s) ? s : null;

    public static bool IsOrdinalWord(string text) => Numeric().IsMatch(text) && text.Any(char.IsLetter) || Words.ContainsKey(text.Trim());

    public static bool LooksLikePlacing(string? text) => Parse(text) is not null;
}
