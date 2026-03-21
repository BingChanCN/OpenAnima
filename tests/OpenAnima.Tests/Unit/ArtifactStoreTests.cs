using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Artifacts;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ArtifactStore"/> using an in-memory SQLite database
/// and a temporary filesystem directory for artifact content files.
/// Each test class instance gets a fresh schema via <see cref="RunDbInitializer.EnsureCreatedAsync"/>.
/// A keepalive connection is held open for the test duration to prevent the in-memory DB from being
/// dropped between operations (required for shared-cache in-memory mode).
/// </summary>
public class ArtifactStoreTests : IDisposable
{
    private const string DbConnectionString = "Data Source=ArtifactStoreTests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly ArtifactFileWriter _fileWriter;
    private readonly ArtifactStore _store;
    private readonly string _tempArtifactsRoot;

    public ArtifactStoreTests()
    {
        // Keep one connection open so the in-memory DB persists for the whole test
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);

        // Schema must exist before any test runs
        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();

        // Create a unique temp directory for artifact files
        _tempArtifactsRoot = Path.Combine(Path.GetTempPath(), $"artifact-test-{Guid.NewGuid()}");
        _fileWriter = new ArtifactFileWriter(_tempArtifactsRoot);
        _store = new ArtifactStore(_factory, _fileWriter, NullLogger<ArtifactStore>.Instance);
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();

        // Clean up temp directory
        if (Directory.Exists(_tempArtifactsRoot))
            Directory.Delete(_tempArtifactsRoot, recursive: true);
    }

    // --- WriteArtifactAsync ---

    [Fact]
    public async Task WriteArtifactAsync_PersistsMetadataAndContent()
    {
        var content = "Hello, world!";
        var artifactId = await _store.WriteArtifactAsync("run1", "step1", "text/plain", content);

        var artifacts = await _store.GetArtifactsByRunIdAsync("run1");
        Assert.Single(artifacts);

        var record = artifacts[0];
        Assert.Equal(artifactId, record.ArtifactId);
        Assert.Equal("run1", record.RunId);
        Assert.Equal("step1", record.StepId);
        Assert.Equal("text/plain", record.MimeType);
        Assert.True(record.FileSizeBytes > 0);
        Assert.False(string.IsNullOrEmpty(record.CreatedAt));

        var readContent = await _store.ReadContentAsync(artifactId);
        Assert.Equal(content, readContent);
    }

    [Fact]
    public async Task WriteArtifactAsync_GeneratesTwelveCharHexId()
    {
        var artifactId = await _store.WriteArtifactAsync("run2", "step2", "text/plain", "data");

        Assert.Equal(12, artifactId.Length);
    }

    [Fact]
    public async Task WriteArtifactAsync_MarkdownMime_CreatesFileWithMdExtension()
    {
        var artifactId = await _store.WriteArtifactAsync("run3", "step3", "text/markdown", "# Header");

        var record = await _store.GetArtifactByIdAsync(artifactId);
        Assert.NotNull(record);
        Assert.True(record!.FilePath.EndsWith(".md"),
            $"Expected FilePath to end with .md but was: {record.FilePath}");
    }

    // --- GetArtifactsByRunIdAsync ---

    [Fact]
    public async Task GetArtifactsByRunIdAsync_NoArtifacts_ReturnsEmptyList()
    {
        var artifacts = await _store.GetArtifactsByRunIdAsync("nonexistent-run");

        Assert.Empty(artifacts);
    }

    // --- GetArtifactByIdAsync ---

    [Fact]
    public async Task GetArtifactByIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _store.GetArtifactByIdAsync("doesnotexist");

        Assert.Null(result);
    }

    // --- DeleteArtifactsByRunAsync ---

    [Fact]
    public async Task DeleteArtifactsByRunAsync_RemovesDbRowsAndFiles()
    {
        var artifactId = await _store.WriteArtifactAsync("run4", "step4", "application/json", "{\"ok\":true}");

        // Verify it exists
        var record = await _store.GetArtifactByIdAsync(artifactId);
        Assert.NotNull(record);

        await _store.DeleteArtifactsByRunAsync("run4");

        // DB rows removed
        var artifacts = await _store.GetArtifactsByRunIdAsync("run4");
        Assert.Empty(artifacts);

        // Directory removed from disk
        var runDir = Path.Combine(_tempArtifactsRoot, "run4");
        Assert.False(Directory.Exists(runDir), $"Expected directory to be removed: {runDir}");
    }

    // --- ReadContentAsync ---

    [Fact]
    public async Task ReadContentAsync_NonExistentArtifact_ReturnsNull()
    {
        var result = await _store.ReadContentAsync("nonexistent-artifact-id");

        Assert.Null(result);
    }
}
