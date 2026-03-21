using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Core.Artifacts;

/// <summary>
/// SQLite + filesystem implementation of <see cref="IArtifactStore"/>.
/// Metadata is persisted in the <c>artifacts</c> SQLite table via Dapper.
/// Content is stored as files under the configured artifacts root directory via <see cref="ArtifactFileWriter"/>.
/// Each public method opens a new connection and disposes it after use — compatible with SQLite WAL mode.
/// </summary>
public class ArtifactStore : IArtifactStore
{
    private readonly RunDbConnectionFactory _factory;
    private readonly ArtifactFileWriter _fileWriter;
    private readonly ILogger<ArtifactStore> _logger;

    /// <summary>
    /// Initializes a new <see cref="ArtifactStore"/>.
    /// </summary>
    /// <param name="factory">Connection factory for the durable run database.</param>
    /// <param name="fileWriter">Filesystem helper for reading and writing artifact content.</param>
    /// <param name="logger">Logger for audit and diagnostic messages.</param>
    public ArtifactStore(
        RunDbConnectionFactory factory,
        ArtifactFileWriter fileWriter,
        ILogger<ArtifactStore> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string> WriteArtifactAsync(
        string runId, string stepId, string mimeType, string content, CancellationToken ct = default)
    {
        var artifactId = Guid.NewGuid().ToString("N")[..12];
        var ext = ArtifactFileWriter.MimeToExtension(mimeType);
        var relativePath = Path.Combine(runId, $"{artifactId}{ext}");

        await _fileWriter.WriteAsync(relativePath, content, ct);

        var record = new ArtifactRecord
        {
            ArtifactId = artifactId,
            RunId = runId,
            StepId = stepId,
            MimeType = mimeType,
            FilePath = relativePath,
            FileSizeBytes = Encoding.UTF8.GetByteCount(content),
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };

        const string sql = """
            INSERT INTO artifacts (artifact_id, run_id, step_id, mime_type, file_path, file_size_bytes, created_at)
            VALUES (@ArtifactId, @RunId, @StepId, @MimeType, @FilePath, @FileSizeBytes, @CreatedAt)
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(sql, record);

        _logger.LogInformation(
            "Wrote artifact {ArtifactId} for run {RunId} step {StepId}",
            artifactId, runId, stepId);

        return artifactId;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ArtifactRecord>> GetArtifactsByRunIdAsync(
        string runId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT artifact_id     AS ArtifactId,
                   run_id          AS RunId,
                   step_id         AS StepId,
                   mime_type       AS MimeType,
                   file_path       AS FilePath,
                   file_size_bytes AS FileSizeBytes,
                   created_at      AS CreatedAt
            FROM artifacts
            WHERE run_id = @RunId
            ORDER BY created_at ASC
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var results = await conn.QueryAsync<ArtifactRecord>(sql, new { RunId = runId });
        return results.ToList();
    }

    /// <inheritdoc/>
    public async Task<ArtifactRecord?> GetArtifactByIdAsync(
        string artifactId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT artifact_id     AS ArtifactId,
                   run_id          AS RunId,
                   step_id         AS StepId,
                   mime_type       AS MimeType,
                   file_path       AS FilePath,
                   file_size_bytes AS FileSizeBytes,
                   created_at      AS CreatedAt
            FROM artifacts
            WHERE artifact_id = @ArtifactId
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<ArtifactRecord>(sql, new { ArtifactId = artifactId });
    }

    /// <inheritdoc/>
    public async Task<string?> ReadContentAsync(string artifactId, CancellationToken ct = default)
    {
        var artifact = await GetArtifactByIdAsync(artifactId, ct);
        if (artifact is null)
            return null;

        return await _fileWriter.ReadAsync(artifact.FilePath, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteArtifactsByRunAsync(string runId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM artifacts WHERE run_id = @RunId";

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(sql, new { RunId = runId });

        await _fileWriter.DeleteDirectoryAsync(runId);

        _logger.LogInformation("Deleted artifacts for run {RunId}", runId);
    }
}
