using HorseShow.Import.Model;
using HorseShow.Import.Normalization;

namespace HorseShow.Import.Mapping;

/// <summary>
/// Proposes a <see cref="ColumnMapping"/>. With a header row it is a synonym lookup. Without one it
/// infers from the values: placings count up, back numbers are short integers that do not, scores
/// are decimals, and the remaining text columns become rider then horse, or a combined column when
/// the values contain " on " / " / ".
/// </summary>
public static class AutoMapper
{
    public static ColumnMapping Propose(ParsedDocument doc)
    {
        var mapping = new ColumnMapping();
        if (doc.HeaderRow is not null)
        {
            for (var c = 0; c < doc.ColumnCount; c++)
            {
                if (ColumnSynonyms.Lookup(doc.HeaderText(c)) is { } field && !mapping.Has(field))
                {
                    mapping.Set(c, field);
                    mapping.Confidence[c] = 0.95;
                }
            }
        }

        // Fill gaps from values, never overriding a header match.
        var textColumns = new List<int>();
        for (var c = 0; c < doc.ColumnCount; c++)
        {
            if (mapping.Fields.ContainsKey(c))
            {
                continue;
            }

            var values = ColumnValues(doc, c);
            if (values.Count == 0)
            {
                continue;
            }

            var guess = Infer(values, doc, c);
            if (guess is { } g && !mapping.Has(g.Field))
            {
                mapping.Set(c, g.Field);
                mapping.Confidence[c] = g.Confidence;
            }
            else if (guess is null && values.Count(v => v.Any(char.IsLetter)) > values.Count / 2)
            {
                textColumns.Add(c);
            }
        }

        // Text columns in order: rider, then horse, then owner.
        var remaining = new Queue<ImportField>(new[] { ImportField.Rider, ImportField.Horse, ImportField.Owner }.Where(f => !mapping.Has(f)));
        if (mapping.Has(ImportField.RiderAndHorse))
        {
            remaining = new Queue<ImportField>(remaining.Where(f => f == ImportField.Owner));
        }

        foreach (var c in textColumns)
        {
            if (remaining.Count == 0)
            {
                break;
            }

            mapping.Set(c, remaining.Dequeue());
            mapping.Confidence[c] = 0.5;
        }

        return mapping;
    }

    private static List<string> ColumnValues(ParsedDocument doc, int column)
        => doc.Blocks.SelectMany(b => b.Rows)
            .Select(r => column < r.Cells.Length ? r.Cells[column].Trim() : string.Empty)
            .Where(v => v.Length > 0)
            .ToList();

    private static (ImportField Field, double Confidence)? Infer(List<string> values, ParsedDocument doc, int column)
    {
        var total = values.Count;
        var placingLike = values.Count(PlacingParser.LooksLikePlacing);
        var ordinal = values.Count(v => PlacingParser.IsOrdinalWord(v));
        var ints = values.Select(v => int.TryParse(v, out var n) ? n : (int?)null).ToList();
        var intCount = ints.Count(n => n is not null);
        var decimals = values.Count(v => v.Contains('.') && decimal.TryParse(v.TrimEnd('%'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _));
        var statusTokens = values.Count(v => PlacingParser.ParseStatusToken(v) is not null);

        if (ordinal + statusTokens > 0 && placingLike == total)
        {
            return (ImportField.Placing, 0.9);
        }

        if (values.Count(v => v.Contains(" on ", StringComparison.OrdinalIgnoreCase) || v.Contains(" / ") || v.Contains(" riding ", StringComparison.OrdinalIgnoreCase)) > total / 2)
        {
            return (ImportField.RiderAndHorse, 0.8);
        }

        if (decimals > total / 2)
        {
            return (ImportField.Score, 0.6);
        }

        if (intCount == total)
        {
            // Placings restart at 1 in every block and stay small; back numbers do not.
            var perBlock = doc.Blocks.Select(b => b.Rows
                .Select(r => column < r.Cells.Length && int.TryParse(r.Cells[column].Trim(), out var n) ? n : (int?)null)
                .Where(n => n is not null).Select(n => n!.Value).ToList()).Where(l => l.Count > 0).ToList();
            var countsUp = perBlock.Count > 0 && perBlock.All(l => l[0] == 1 && l.Zip(l.Skip(1), (a, b) => b >= a).All(x => x));
            var small = ints.All(n => n is >= 1 and <= 30);
            if (countsUp && small)
            {
                return (ImportField.Placing, 0.75);
            }

            if (ints.All(n => n is >= 1 and <= 9999))
            {
                return (ImportField.BackNumber, 0.55);
            }
        }

        return null;
    }
}
