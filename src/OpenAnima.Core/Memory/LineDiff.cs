namespace OpenAnima.Core.Memory;

/// <summary>
/// Computes line-level diffs between two text strings.
/// Uses a simple LCS (longest common subsequence) approach suitable for
/// memory node content (typically under 500 lines).
/// </summary>
public static class LineDiff
{
    /// <summary>The type of change for a diff line.</summary>
    public enum DiffKind
    {
        /// <summary>Line exists in both old and new text.</summary>
        Unchanged,
        /// <summary>Line exists only in the new text (added).</summary>
        Added,
        /// <summary>Line exists only in the old text (removed).</summary>
        Removed
    }

    /// <summary>
    /// Computes a line-level diff between <paramref name="oldText"/> and <paramref name="newText"/>.
    /// Returns a list of (DiffKind, Line) tuples representing the unified diff.
    /// </summary>
    /// <param name="oldText">The older content.</param>
    /// <param name="newText">The newer content.</param>
    /// <returns>Ordered list of diff entries.</returns>
    public static IReadOnlyList<(DiffKind Kind, string Line)> Compute(string oldText, string newText)
    {
        var oldRaw = oldText ?? "";
        var newRaw = newText ?? "";
        var oldLines = oldRaw.Length == 0 ? Array.Empty<string>() : oldRaw.Split('\n');
        var newLines = newRaw.Length == 0 ? Array.Empty<string>() : newRaw.Split('\n');

        // Build LCS table
        var m = oldLines.Length;
        var n = newLines.Length;
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (oldLines[i - 1] == newLines[j - 1])
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
            }
        }

        // Backtrack to produce diff
        var result = new List<(DiffKind, string)>();
        int oi = m, ni = n;

        while (oi > 0 || ni > 0)
        {
            if (oi > 0 && ni > 0 && oldLines[oi - 1] == newLines[ni - 1])
            {
                result.Add((DiffKind.Unchanged, oldLines[oi - 1]));
                oi--;
                ni--;
            }
            else if (ni > 0 && (oi == 0 || dp[oi, ni - 1] >= dp[oi - 1, ni]))
            {
                result.Add((DiffKind.Added, newLines[ni - 1]));
                ni--;
            }
            else
            {
                result.Add((DiffKind.Removed, oldLines[oi - 1]));
                oi--;
            }
        }

        result.Reverse();
        return result;
    }
}
