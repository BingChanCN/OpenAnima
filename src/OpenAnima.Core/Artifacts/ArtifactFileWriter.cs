namespace OpenAnima.Core.Artifacts;

/// <summary>
/// Handles filesystem read and write operations for artifact content files.
/// All paths are relative to the configured artifacts root directory.
/// Path traversal attempts are rejected by validating that resolved paths remain under the root.
/// </summary>
public class ArtifactFileWriter
{
    private readonly string _artifactsRoot;

    /// <summary>
    /// Initializes a new <see cref="ArtifactFileWriter"/> using the given root directory.
    /// </summary>
    /// <param name="artifactsRoot">Absolute path to the <c>data/artifacts/</c> directory.</param>
    public ArtifactFileWriter(string artifactsRoot)
    {
        _artifactsRoot = artifactsRoot ?? throw new ArgumentNullException(nameof(artifactsRoot));
    }

    /// <summary>
    /// Writes content to the file at the given relative path, creating directories as needed.
    /// </summary>
    /// <param name="relativePath">Relative path from the artifacts root (e.g., <c>{runId}/{artifactId}.txt</c>).</param>
    /// <param name="content">Text content to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the resolved path escapes the artifacts root.</exception>
    public async Task WriteAsync(string relativePath, string content, CancellationToken ct = default)
    {
        var fullPath = ResolveAndValidate(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, ct);
    }

    /// <summary>
    /// Reads content from the file at the given relative path.
    /// Returns <c>null</c> if the file does not exist.
    /// </summary>
    /// <param name="relativePath">Relative path from the artifacts root.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string?> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolveAndValidate(relativePath);
        if (!File.Exists(fullPath))
            return null;
        return await File.ReadAllTextAsync(fullPath, ct);
    }

    /// <summary>
    /// Deletes the directory for the given run ID and all its contents.
    /// No-ops if the directory does not exist.
    /// </summary>
    /// <param name="runId">The run ID whose artifact directory should be removed.</param>
    public Task DeleteDirectoryAsync(string runId)
    {
        var dirPath = Path.Combine(_artifactsRoot, runId);
        if (Directory.Exists(dirPath))
            Directory.Delete(dirPath, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps a MIME type to the appropriate file extension.
    /// </summary>
    /// <param name="mimeType">The MIME type string.</param>
    /// <returns>The file extension including the leading dot (e.g., <c>.txt</c>).</returns>
    public static string MimeToExtension(string mimeType) => mimeType switch
    {
        "text/plain"       => ".txt",
        "text/markdown"    => ".md",
        "application/json" => ".json",
        "text/html"        => ".html",
        _                  => ".bin"
    };

    // ── Private helpers ──────────────────────────────────────────────────────

    private string ResolveAndValidate(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_artifactsRoot, relativePath));
        var root = Path.GetFullPath(_artifactsRoot);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path '{relativePath}' escapes the artifacts root.");
        return fullPath;
    }
}
