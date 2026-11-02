using System.Text.RegularExpressions;
using HorseShow.Import.Model;

namespace HorseShow.Import.Parsing;

/// <summary>
/// Recognises lines that introduce a class. Formats seen in the wild and covered by fixtures:
///   "Class 12 - Hunter Under Saddle", "Class 12: Hunter Under Saddle (8 entries)",
///   "#12 Hunter Under Saddle", "12. Hunter Under Saddle", "Hunter Under Saddle (8)",
///   "Class 12", "Hunter Under Saddle - 8 entries".
/// To support a new source, extend the patterns here and add a fixture.
/// </summary>
public static partial class ClassHeaderParser
{
    [GeneratedRegex(@"^\s*(?:class|cls|event)?\s*#?\s*(?<num>\d{1,4}[A-Za-z]?)\s*(?:[-–—:.)]\s*)?(?<name>.*?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Numbered();

    [GeneratedRegex(@"\(?\s*(?<n>\d{1,3})\s*(?:entries|entered|entry|shown|in class|horses|riders|starters)\s*\)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingEntries();

    [GeneratedRegex(@"\(\s*(?<n>\d{1,3})\s*\)\s*$")]
    private static partial Regex TrailingParenNumber();

    [GeneratedRegex(@"^\s*(?:entries|entered|shown)\s*[:=]\s*(?<n>\d{1,3})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex EntriesOnly();

    [GeneratedRegex(@"^\s*(?:class|cls|event)\s*#?\s*\d", RegexOptions.IgnoreCase)]
    private static partial Regex StartsWithClassWord();

    /// <summary>Parses a candidate line. Returns null when it does not look like a class header.</summary>
    public static ClassHeader? Parse(string text, int lineNumber)
    {
        var s = text.Trim().Trim('"');
        if (s.Length == 0)
        {
            return null;
        }

        int? entries = null;
        var m = TrailingEntries().Match(s);
        if (m.Success)
        {
            entries = int.Parse(m.Groups["n"].Value);
            s = s[..m.Index].TrimEnd(' ', '-', '–', '—', ':', ',', '(');
        }
        else
        {
            m = TrailingParenNumber().Match(s);
            if (m.Success)
            {
                entries = int.Parse(m.Groups["n"].Value);
                s = s[..m.Index].TrimEnd();
            }
        }

        if (StartsWithClassWord().IsMatch(s))
        {
            var n = Numbered().Match(s);
            return new ClassHeader(n.Groups["num"].Value, Clean(n.Groups["name"].Value), entries, lineNumber);
        }

        var numbered = Numbered().Match(s);
        if (numbered.Success && numbered.Groups["num"].Success && LooksLikeName(numbered.Groups["name"].Value))
        {
            return new ClassHeader(numbered.Groups["num"].Value, Clean(numbered.Groups["name"].Value), entries, lineNumber);
        }

        // Bare name: only convincing with an entry count ("Hunter Under Saddle (8)") or when it is clearly a
        // title of three or more words. Two words alone ("Jane Smith") are a name, not a class.
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (LooksLikeName(s) && s.Count(char.IsLetter) >= 4 && (entries is not null || words >= 3))
        {
            return new ClassHeader(null, Clean(s), entries, lineNumber);
        }

        return null;
    }

    /// <summary>True when the line announces itself with "Class", "Event" or "#": safe to treat as a header even inside a table.</summary>
    public static bool IsExplicit(string text) => StartsWithClassWord().IsMatch(text.Trim()) || text.TrimStart().StartsWith('#');

    /// <summary>"Entries: 8" on its own line, applying to the current class.</summary>
    public static int? ParseEntriesOnly(string text)
    {
        var m = EntriesOnly().Match(text.Trim());
        return m.Success ? int.Parse(m.Groups["n"].Value) : null;
    }

    private static bool LooksLikeName(string s)
        => s.Length >= 3 && s.Any(char.IsLetter) && !s.All(c => char.IsDigit(c) || char.IsWhiteSpace(c) || c == '.');

    private static string? Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        return s.Trim().Trim('-', '–', '—', ':', '.', ' ');
    }
}
