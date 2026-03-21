namespace OpenAnima.Core.Runs;

/// <summary>
/// Maps a PropagationId to a deterministic color hex string for UI visualization.
/// Cycles through a fixed 8-color palette using the propagation ID's hash code.
/// </summary>
public static class PropagationColorAssigner
{
    private static readonly string[] Colors =
    [
        "#6c8cff",
        "#4ade80",
        "#fbbf24",
        "#f87171",
        "#a78bfa",
        "#34d399",
        "#fb923c",
        "#60a5fa"
    ];

    /// <summary>
    /// Returns a deterministic hex color for the given propagation ID.
    /// Returns "transparent" for null or empty input.
    /// </summary>
    public static string GetColor(string? propagationId)
    {
        if (string.IsNullOrEmpty(propagationId))
            return "transparent";

        return Colors[Math.Abs(propagationId.GetHashCode()) % Colors.Length];
    }
}
