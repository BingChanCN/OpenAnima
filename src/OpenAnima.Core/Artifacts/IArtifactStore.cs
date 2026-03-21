namespace OpenAnima.Core.Artifacts;

/// <summary>
/// Abstraction for reading and writing durable artifact data.
/// Artifacts are content files linked to run steps — full text content is stored on
/// the filesystem while metadata is persisted in the SQLite <c>artifacts</c> table.
/// </summary>
public interface IArtifactStore
{
    /// <summary>
    /// Writes artifact content to disk and persists its metadata to SQLite.
    /// The artifact ID is generated internally as a 12-character hex string.
    /// </summary>
    /// <param name="runId">The run this artifact belongs to.</param>
    /// <param name="stepId">The step that produced this artifact.</param>
    /// <param name="mimeType">MIME type of the content (e.g., "text/plain", "application/json").</param>
    /// <param name="content">The full text content to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated artifact ID.</returns>
    Task<string> WriteArtifactAsync(string runId, string stepId, string mimeType, string content, CancellationToken ct = default);

    /// <summary>
    /// Returns all artifacts for the given run, ordered by creation time ascending (oldest first).
    /// </summary>
    /// <param name="runId">The run whose artifacts to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ArtifactRecord>> GetArtifactsByRunIdAsync(string runId, CancellationToken ct = default);

    /// <summary>
    /// Returns the artifact with the given ID, or <c>null</c> if it does not exist.
    /// </summary>
    /// <param name="artifactId">The artifact ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ArtifactRecord?> GetArtifactByIdAsync(string artifactId, CancellationToken ct = default);

    /// <summary>
    /// Reads the content of the artifact with the given ID from disk.
    /// Returns <c>null</c> if the artifact does not exist or the file cannot be found.
    /// </summary>
    /// <param name="artifactId">The artifact whose content to read.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> ReadContentAsync(string artifactId, CancellationToken ct = default);

    /// <summary>
    /// Deletes all artifact metadata rows for the given run from SQLite and removes
    /// the corresponding <c>data/artifacts/{runId}/</c> directory from disk.
    /// </summary>
    /// <param name="runId">The run whose artifacts to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteArtifactsByRunAsync(string runId, CancellationToken ct = default);
}
