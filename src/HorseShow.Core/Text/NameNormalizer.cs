using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HorseShow.Core.Text;

/// <summary>
/// Produces the canonical key used for alias lookups and import matching.
/// One implementation, shared by the alias unique index population and the importer,
/// so a name that matched once keeps matching.
/// </summary>
public static partial class NameNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^\p{L}\p{N}\s]")]
    private static partial Regex Punctuation();

    /// <summary>
    /// Trims, collapses whitespace, strips punctuation and diacritics, casefolds, and
    /// rewrites "Last, First" as "first last".
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var s = input.Trim();

        // "Smith, Jane" -> "Jane Smith". Only a single comma is treated as the surname separator.
        var comma = s.IndexOf(',');
        if (comma > 0 && s.IndexOf(',', comma + 1) < 0)
        {
            var last = s[..comma].Trim();
            var first = s[(comma + 1)..].Trim();
            if (first.Length > 0 && last.Length > 0)
            {
                s = $"{first} {last}";
            }
        }

        s = RemoveDiacritics(s);
        s = Punctuation().Replace(s, " ");
        s = Whitespace().Replace(s, " ").Trim();
        return s.ToLowerInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
