using FuzzySharp;
using HorseShow.Core.Model;
using HorseShow.Core.Text;
using HorseShow.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseShow.Import.Resolution;

/// <summary>
/// Exact alias match first, then FuzzySharp token-sort ratio against every alias of every
/// non-archived rider or horse. The data set is one barn or circuit, so loading all aliases
/// is cheaper than being clever.
/// </summary>
public sealed class NameResolver(int suggestThreshold = 86, int candidateFloor = 70)
{
    public async Task<List<NameResolution>> ResolveAsync(HorseShowDbContext db, ParticipantKind kind, IEnumerable<string> texts, CancellationToken ct = default)
    {
        var aliases = kind == ParticipantKind.Rider
            ? await db.RiderAliases.AsNoTracking().Where(a => !a.Rider!.IsArchived)
                .Select(a => new AliasRow(a.RiderId, a.Rider!.DisplayName ?? a.Rider.Name, a.Alias)).ToListAsync(ct)
            : await db.HorseAliases.AsNoTracking().Where(a => !a.Horse!.IsArchived)
                .Select(a => new AliasRow(a.HorseId, a.Horse!.Name, a.Alias)).ToListAsync(ct);

        var exact = aliases.GroupBy(a => a.Alias).ToDictionary(g => g.Key, g => g.First());
        var results = new List<NameResolution>();

        foreach (var group in texts.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).GroupBy(NameNormalizer.Normalize))
        {
            var normalized = group.Key;
            var text = group.First();
            var occurrences = group.Count();

            if (exact.TryGetValue(normalized, out var hit))
            {
                results.Add(new NameResolution
                {
                    Kind = kind, Text = text, Normalized = normalized, Status = ResolutionStatus.Exact, Occurrences = occurrences,
                    Candidates = [new Candidate(hit.Id, hit.Name, 100)], ChosenId = hit.Id, ChosenName = hit.Name,
                });
                continue;
            }

            var scored = aliases
                .Select(a => (a.Id, a.Name, Score: Fuzz.TokenSortRatio(normalized, a.Alias)))
                .Where(x => x.Score >= candidateFloor)
                .GroupBy(x => x.Id)
                .Select(g => new Candidate(g.Key, g.First().Name, g.Max(x => x.Score)))
                .OrderByDescending(c => c.Score).ThenBy(c => c.Name)
                .Take(3)
                .ToList();

            var best = scored.FirstOrDefault();
            var status = best is not null && best.Score >= suggestThreshold ? ResolutionStatus.Suggested : ResolutionStatus.New;
            results.Add(new NameResolution
            {
                Kind = kind, Text = text, Normalized = normalized, Status = status, Occurrences = occurrences, Candidates = scored,
                ChosenId = status == ResolutionStatus.Suggested ? best!.Id : null,
                ChosenName = status == ResolutionStatus.Suggested ? best!.Name : null,
            });
        }

        return results.OrderBy(r => r.Status == ResolutionStatus.Exact ? 2 : r.Status == ResolutionStatus.Suggested ? 0 : 1).ThenBy(r => r.Text).ToList();
    }

    private sealed record AliasRow(int Id, string Name, string Alias);
}
