using System.Text;
using Thiccdal.Data.Models;

namespace Thiccdal.Data;

internal static class UserIdentitySuggestionMatcher
{
    private static readonly string[] CommonAffixes =
    [
        "ttv",
        "yt",
        "youtube",
        "twitch",
        "live"
    ];

    public static double CalculateSimilarity(string leftDisplayName, string rightDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leftDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightDisplayName);

        string left = Normalize(leftDisplayName);
        string right = Normalize(rightDisplayName);

        if (left.Length < 3 || right.Length < 3)
        {
            return 0d;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1d;
        }

        int maxLength = Math.Max(left.Length, right.Length);
        int distance = ComputeLevenshteinDistance(left, right);
        return 1d - (double)distance / maxLength;
    }

    private static string Normalize(string displayName)
    {
        StringBuilder builder = new(displayName.Length);
        foreach (char character in displayName.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        string normalized = builder.ToString();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        normalized = TrimRepeatedEdgeCharacter(normalized, 'x');

        bool changed;
        do
        {
            changed = false;

            foreach (string affix in CommonAffixes)
            {
                if (normalized.Length > affix.Length + 2 &&
                    normalized.StartsWith(affix, StringComparison.Ordinal))
                {
                    normalized = normalized[affix.Length..];
                    changed = true;
                }

                if (normalized.Length > affix.Length + 2 &&
                    normalized.EndsWith(affix, StringComparison.Ordinal))
                {
                    normalized = normalized[..^affix.Length];
                    changed = true;
                }
            }

            normalized = TrimRepeatedEdgeCharacter(normalized, 'x');
        }
        while (changed && normalized.Length > 0);

        return normalized;
    }

    private static string TrimRepeatedEdgeCharacter(string value, char character)
    {
        string trimmed = value;

        while (trimmed.Length > 3 && trimmed.StartsWith($"{character}{character}", StringComparison.Ordinal))
        {
            trimmed = trimmed.TrimStart(character);
        }

        while (trimmed.Length > 3 && trimmed.EndsWith($"{character}{character}", StringComparison.Ordinal))
        {
            trimmed = trimmed.TrimEnd(character);
        }

        return trimmed;
    }

    private static int ComputeLevenshteinDistance(string left, string right)
    {
        int[,] distances = new int[left.Length + 1, right.Length + 1];

        for (int index = 0; index <= left.Length; index++)
        {
            distances[index, 0] = index;
        }

        for (int index = 0; index <= right.Length; index++)
        {
            distances[0, index] = index;
        }

        for (int leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            for (int rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                int substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                distances[leftIndex, rightIndex] = Math.Min(
                    Math.Min(
                        distances[leftIndex - 1, rightIndex] + 1,
                        distances[leftIndex, rightIndex - 1] + 1),
                    distances[leftIndex - 1, rightIndex - 1] + substitutionCost);
            }
        }

        return distances[left.Length, right.Length];
    }
}
