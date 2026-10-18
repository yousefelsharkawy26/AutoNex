using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Provides self-healing element resolution for agents.
/// If an agent guesses or provides an approximate AutomationId, Name, or locator,
/// this engine calculates string distance / similarity to find the most likely match,
/// eliminating trial-and-error retry loops and conserving LLM tokens.
/// </summary>
public static class FuzzyLocator
{
    /// <summary>
    /// The canonical set of action types understood by the batch runner.
    /// </summary>
    public static readonly string[] KnownActionTypes =
    {
        "click", "double_click", "right_click", "hover", "type", "paste", "clear", "check", "uncheck",
        "select", "press_key", "send_keys", "scroll", "scroll_into_view", "wait", "assert", "assert_text"
    };

    /// <summary>
    /// Suggests the closest known action type for a (possibly misspelled) action name,
    /// e.g. "clik" -> "click". Returns null when nothing is close enough.
    /// Helps agents self-correct typos instead of burning tokens on retry loops.
    /// </summary>
    public static string? SuggestActionType(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        var normalized = Normalize(action);
        string? best = null;
        double bestScore = 0.6; // require a reasonably strong match before suggesting

        foreach (var candidate in KnownActionTypes)
        {
            var score = CalculateSimilarity(normalized, Normalize(candidate));
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Matches an approximate locator against a list of candidates.
    /// Returns the best matching AutomationId or element Name if similarity exceeds threshold.
    /// </summary>
    /// <param name="target">The locator requested by the agent (e.g. "submit_btn" or "firstName").</param>
    /// <param name="candidates">Available elements extracted from the UI tree.</param>
    /// <param name="minScore">Minimum similarity score (0.0 to 1.0) to consider a valid match. Default is 0.55.</param>
    /// <returns>The resolved AutomationId or Name, or null if no confident match found.</returns>
    public static string? ResolveBestMatch(string target, IEnumerable<CompactUiElement> candidates, double minScore = 0.55)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        var normalizedTarget = Normalize(target);
        CompactUiElement? bestCandidate = null;
        double highestScore = -1.0;

        foreach (var candidate in candidates)
        {
            // Exact matches always win regardless of threshold.
            if (string.Equals(candidate.AutomationId, target, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Name, target, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.AutomationId ?? candidate.Name;
            }

            var idScore = !string.IsNullOrEmpty(candidate.AutomationId) 
                ? CalculateSimilarity(normalizedTarget, Normalize(candidate.AutomationId)) 
                : 0.0;

            var nameScore = !string.IsNullOrEmpty(candidate.Name) 
                ? CalculateSimilarity(normalizedTarget, Normalize(candidate.Name)) 
                : 0.0;

            // Containment boost: normalized target contained in (or containing) the
            // candidate identifier. Handles cases like "submit" -> "SubmitButton"
            // where Levenshtein alone scores too low due to length differences.
            var containmentBonus = 0.0;
            if (IsContainmentMatch(normalizedTarget, candidate.AutomationId) ||
                IsContainmentMatch(normalizedTarget, candidate.Name))
            {
                containmentBonus = 0.35;
            }

            var score = Math.Max(idScore, nameScore) + containmentBonus;

            if (score > highestScore)
            {
                highestScore = score;
                bestCandidate = candidate;
            }
        }

        if (highestScore >= minScore && bestCandidate != null)
        {
            return bestCandidate.AutomationId ?? bestCandidate.Name;
        }

        return null;
    }

    /// <summary>
    /// Normalizes identifiers for fuzzy comparison:
    /// Strips spaces, underscores, hyphens, and common suffixes (btn, button, txt, textbox, input, lbl, label).
    /// </summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var cleaned = input.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");

        // Strip common control noise suffixes
        string[] noiseSuffixes = ["button", "btn", "textbox", "txt", "input", "label", "lbl", "cb", "combo", "combobox"];
        foreach (var suffix in noiseSuffixes)
        {
            if (cleaned.Length > suffix.Length && cleaned.EndsWith(suffix))
            {
                cleaned = cleaned[..^suffix.Length];
                break;
            }
        }

        return cleaned;
    }

    /// <summary>
    /// Returns true when the normalized target is a meaningful substring of the
    /// candidate identifier (or vice versa), e.g. "submit" inside "submitbutton".
    /// Requires at least 3 characters to avoid noisy single/two-letter matches.
    /// </summary>
    private static bool IsContainmentMatch(string normalizedTarget, string? candidateIdentifier)
    {
        if (string.IsNullOrEmpty(candidateIdentifier))
        {
            return false;
        }

        var normalizedCandidate = Normalize(candidateIdentifier);

        if (normalizedTarget.Length >= 3 &&
            (normalizedCandidate.Contains(normalizedTarget, StringComparison.Ordinal) ||
             normalizedTarget.Contains(normalizedCandidate, StringComparison.Ordinal)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates similarity score between two strings using Levenshtein distance.
    /// Returns a value between 0.0 (completely different) and 1.0 (exact match).
    /// </summary>
    public static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
        {
            return 0.0;
        }

        if (s1 == s2)
        {
            return 1.0;
        }

        // Substring / Contains bonus
        if (s1.Contains(s2) || s2.Contains(s1))
        {
            var ratio = (double)Math.Min(s1.Length, s2.Length) / Math.Max(s1.Length, s2.Length);
            return Math.Max(0.7, ratio);
        }

        int distance = ComputeLevenshteinDistance(s1, s2);
        int maxLength = Math.Max(s1.Length, s2.Length);

        return 1.0 - ((double)distance / maxLength);
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}

