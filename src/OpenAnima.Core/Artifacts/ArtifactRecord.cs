namespace OpenAnima.Core.Artifacts;

/// <summary>
/// Immutable record representing a single persisted artifact linked to a run step.
/// Maps to a row in the <c>artifacts</c> table. Each artifact stores metadata about
/// a content file written to the <c>data/artifacts/{runId}/</c> directory.
/// </summary>
public record ArtifactRecord
{
    /// <summary>
    /// 12-character hexadecimal identifier for this artifact.
    /// Generated as <c>Guid.NewGuid().ToString("N")[..12]</c>.
    /// </summary>
    public string ArtifactId { get; init; } = string.Empty;

    /// <summary>Identifier of the run this artifact belongs to.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Identifier of the step that produced this artifact.</summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>MIME type of the artifact content (e.g., "text/plain", "application/json").</summary>
    public string MimeType { get; init; } = string.Empty;

    /// <summary>
    /// Relative path from the artifacts root to the content file.
    /// Format: <c>{runId}/{artifactId}.ext</c>
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Size of the content in bytes (UTF-8 encoded).</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>ISO 8601 UTC timestamp when this artifact was created.</summary>
    public string CreatedAt { get; init; } = string.Empty;
}
