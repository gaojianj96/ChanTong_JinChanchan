namespace ChanSight.Vision.Models;

using ChanSight.Core.Season;

public static class GameSeasonDictionary
{
    /// <summary>英雄全表: 与 <see cref="SeasonDictionarySeed.Heroes"/> 同源(S16.5 种子)。</summary>
    public static readonly IReadOnlySet<string> Heroes = SeasonDictionarySeed.Heroes;

    /// <summary>羁绊全表: 与 <see cref="SeasonDictionarySeed.Traits"/> 同源(S16.5 种子)。</summary>
    public static readonly IReadOnlySet<string> Traits = SeasonDictionarySeed.Traits;

    /// <summary>装备全表: 与 <see cref="SeasonDictionarySeed.Items"/> 同源(S16.5 种子)。</summary>
    public static readonly IReadOnlySet<string> Items = SeasonDictionarySeed.Items;

    public static readonly IReadOnlySet<string> Numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"
    };

    public static int ComputeLevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        int lenA = a.Length;
        int lenB = b.Length;

        if (lenA == 0) return lenB;
        if (lenB == 0) return lenA;

        Span<int> prevRow = stackalloc int[lenB + 1];
        Span<int> currRow = stackalloc int[lenB + 1];

        for (int j = 0; j <= lenB; j++)
            prevRow[j] = j;

        for (int i = 1; i <= lenA; i++)
        {
            currRow[0] = i;
            for (int j = 1; j <= lenB; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                int deletion = prevRow[j] + 1;
                int insertion = currRow[j - 1] + 1;
                int substitution = prevRow[j - 1] + cost;

                currRow[j] = Math.Min(deletion, Math.Min(insertion, substitution));
            }

            Span<int> temp = prevRow;
            prevRow = currRow;
            currRow = temp;
        }

        return prevRow[lenB];
    }

    public static string? TryFuzzyMatch(string text, IReadOnlySet<string> dictionary, int maxDistance = 2)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.AsSpan().Trim();

        var trimmedString = trimmed.ToString();
        if (dictionary.Contains(trimmedString))
            return trimmedString;

        string? bestMatch = null;
        int bestDistance = int.MaxValue;

        foreach (var entry in dictionary)
        {
            int distance = ComputeLevenshteinDistance(trimmed, entry.AsSpan());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestMatch = entry;
            }

            if (bestDistance == 0)
                break;
        }

        return bestDistance <= maxDistance ? bestMatch : null;
    }

    public static int? TryParseNumeric(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '-').ToArray());

        if (int.TryParse(cleaned, out int result))
            return result;

        return null;
    }
}