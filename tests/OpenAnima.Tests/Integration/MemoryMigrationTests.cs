using Dapper;
using Microsoft.Data.Sqlite;
using OpenAnima.Core.RunPersistence;
using Xunit;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Tests that the Phase 65 migration from old 3-table memory model to new 4-table model
/// preserves all data without loss.
/// </summary>
public class MemoryMigrationTests : IDisposable
{
    private readonly string _dbName;
    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;

    public MemoryMigrationTests()
    {
        _dbName = $"MigrationTest_{Guid.NewGuid():N}";
        var connStr = $"Data Source={_dbName};Mode=Memory;Cache=Shared";
        _factory = new RunDbConnectionFactory(connStr, isRaw: true);

        // Keep a connection alive so the in-memory database persists between
        // the seeding phase and the migration phase.
        _keepAlive = new SqliteConnection(connStr);
        _keepAlive.Open();
    }

    public void Dispose()
    {
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task MigratePreservesAllData()
    {
        // Arrange: Create OLD schema and seed test data
        await using (var conn = _factory.CreateConnection())
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("PRAGMA journal_mode=WAL;");
            await conn.ExecuteAsync("PRAGMA synchronous=NORMAL;");

            // Create old 3-table schema
            await conn.ExecuteAsync("""
                CREATE TABLE memory_nodes (
                    uri TEXT NOT NULL,
                    anima_id TEXT NOT NULL,
                    content TEXT NOT NULL,
                    disclosure_trigger TEXT,
                    keywords TEXT,
                    source_artifact_id TEXT,
                    source_step_id TEXT,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    PRIMARY KEY (uri, anima_id)
                );
                CREATE TABLE memory_edges (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    anima_id TEXT NOT NULL,
                    from_uri TEXT NOT NULL,
                    to_uri TEXT NOT NULL,
                    label TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE TABLE memory_snapshots (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    uri TEXT NOT NULL,
                    anima_id TEXT NOT NULL,
                    content TEXT NOT NULL,
                    snapshot_at TEXT NOT NULL
                );
                CREATE TABLE runs (
                    run_id TEXT NOT NULL PRIMARY KEY,
                    anima_id TEXT NOT NULL,
                    objective TEXT NOT NULL,
                    workspace_root TEXT NOT NULL,
                    max_steps INTEGER,
                    max_wall_seconds INTEGER,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    workflow_preset TEXT
                );
                CREATE TABLE run_state_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    run_id TEXT NOT NULL,
                    state TEXT NOT NULL,
                    reason TEXT,
                    occurred_at TEXT NOT NULL
                );
                CREATE TABLE step_events (
                    step_id TEXT NOT NULL PRIMARY KEY,
                    run_id TEXT NOT NULL,
                    propagation_id TEXT NOT NULL,
                    module_name TEXT NOT NULL,
                    status TEXT NOT NULL,
                    input_summary TEXT,
                    output_summary TEXT,
                    artifact_ref_id TEXT,
                    error_info TEXT,
                    duration_ms INTEGER,
                    occurred_at TEXT NOT NULL
                );
                CREATE TABLE artifacts (
                    artifact_id TEXT NOT NULL PRIMARY KEY,
                    run_id TEXT NOT NULL,
                    step_id TEXT NOT NULL,
                    mime_type TEXT NOT NULL,
                    file_path TEXT NOT NULL,
                    file_size_bytes INTEGER NOT NULL,
                    created_at TEXT NOT NULL
                );
                """);

            // Seed test data
            await conn.ExecuteAsync("""
                INSERT INTO memory_nodes VALUES ('core://agent/identity', 'anima-1', 'I am an AI assistant', 'identity', '["ai","assistant"]', NULL, NULL, '2025-01-01T00:00:00Z', '2025-06-01T00:00:00Z');
                INSERT INTO memory_nodes VALUES ('sediment://fact/uses-blazor', 'anima-1', 'Project uses Blazor', NULL, '["blazor","project"]', 'art-1', 'step-1', '2025-02-01T00:00:00Z', '2025-03-01T00:00:00Z');
                INSERT INTO memory_nodes VALUES ('sediment://preference/dark-mode', 'anima-1', 'User prefers dark mode', 'dark mode', NULL, NULL, NULL, '2025-04-01T00:00:00Z', '2025-04-01T00:00:00Z');
                INSERT INTO memory_nodes VALUES ('run://r1/findings', 'anima-1', 'Run findings text', NULL, NULL, NULL, 'step-2', '2025-05-01T00:00:00Z', '2025-05-01T00:00:00Z');
                INSERT INTO memory_edges VALUES (1, 'anima-1', 'core://agent/identity', 'sediment://fact/uses-blazor', 'related-to', '2025-03-01T00:00:00Z');
                INSERT INTO memory_edges VALUES (2, 'anima-1', 'sediment://fact/uses-blazor', 'sediment://preference/dark-mode', 'influences', '2025-04-01T00:00:00Z');
                INSERT INTO memory_snapshots VALUES (1, 'core://agent/identity', 'anima-1', 'Old identity content v1', '2025-03-01T00:00:00Z');
                INSERT INTO memory_snapshots VALUES (2, 'core://agent/identity', 'anima-1', 'Old identity content v2', '2025-05-01T00:00:00Z');
                """);
        }

        // Act: Run EnsureCreatedAsync which triggers migration
        var initializer = new RunDbInitializer(_factory);
        await initializer.EnsureCreatedAsync();

        // Assert: Verify all data migrated correctly
        await using var conn2 = _factory.CreateConnection();
        await conn2.OpenAsync();

        // 4 nodes migrated
        var nodeCount = await conn2.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM memory_nodes");
        Assert.Equal(4, nodeCount);

        // All nodes have UUIDs (36-char format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
        var uuids = (await conn2.QueryAsync<string>("SELECT uuid FROM memory_nodes")).ToList();
        Assert.All(uuids, uuid => Assert.Equal(36, uuid.Length));

        // Node types inferred correctly
        var systemNode = await conn2.QueryFirstAsync<dynamic>(
            "SELECT n.node_type, p.uri FROM memory_nodes n JOIN memory_uri_paths p ON n.uuid = p.node_uuid WHERE p.uri = 'core://agent/identity'");
        Assert.Equal("System", (string)systemNode.node_type);

        var factNode = await conn2.QueryFirstAsync<dynamic>(
            "SELECT n.node_type, n.display_name FROM memory_nodes n JOIN memory_uri_paths p ON n.uuid = p.node_uuid WHERE p.uri = 'sediment://fact/uses-blazor'");
        Assert.Equal("Fact", (string)factNode.node_type);
        Assert.Equal("uses-blazor", (string)factNode.display_name);

        var prefNode = await conn2.QueryFirstAsync<dynamic>(
            "SELECT n.node_type FROM memory_nodes n JOIN memory_uri_paths p ON n.uuid = p.node_uuid WHERE p.uri = 'sediment://preference/dark-mode'");
        Assert.Equal("Preference", (string)prefNode.node_type);

        var artifactNode = await conn2.QueryFirstAsync<dynamic>(
            "SELECT n.node_type FROM memory_nodes n JOIN memory_uri_paths p ON n.uuid = p.node_uuid WHERE p.uri = 'run://r1/findings'");
        Assert.Equal("Artifact", (string)artifactNode.node_type);

        // 4 URI paths created (one per node)
        var pathCount = await conn2.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM memory_uri_paths");
        Assert.Equal(4, pathCount);

        // Content preserved: 4 current content rows + 2 snapshot rows = 6 total
        var contentCount = await conn2.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM memory_contents");
        Assert.Equal(6, contentCount);

        // Current content matches original
        var identityUuid = await conn2.ExecuteScalarAsync<string>(
            "SELECT node_uuid FROM memory_uri_paths WHERE uri = 'core://agent/identity'");
        var latestContent = await conn2.QueryFirstAsync<string>(
            "SELECT content FROM memory_contents WHERE node_uuid = @Uuid ORDER BY id DESC LIMIT 1",
            new { Uuid = identityUuid });
        Assert.Equal("I am an AI assistant", latestContent);

        // Disclosure trigger preserved on content row
        var identityTrigger = await conn2.QueryFirstAsync<string>(
            "SELECT disclosure_trigger FROM memory_contents WHERE node_uuid = @Uuid ORDER BY id DESC LIMIT 1",
            new { Uuid = identityUuid });
        Assert.Equal("identity", identityTrigger);

        // 2 edges migrated with UUID references
        var edgeCount = await conn2.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM memory_edges");
        Assert.Equal(2, edgeCount);

        // Edge UUIDs point to valid nodes
        var edges = (await conn2.QueryAsync<dynamic>("SELECT parent_uuid, child_uuid, label FROM memory_edges ORDER BY id")).ToList();
        Assert.Equal("related-to", (string)edges[0].label);
        Assert.Equal("influences", (string)edges[1].label);

        // Old tables dropped
        var tables = (await conn2.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='table'")).ToList();
        Assert.DoesNotContain("memory_nodes_old", tables);
        Assert.DoesNotContain("memory_edges_old", tables);
        Assert.DoesNotContain("memory_snapshots", tables);
    }

    [Fact]
    public async Task FreshInstallCreatesNewSchemaDirectly()
    {
        // Act: Run EnsureCreatedAsync on empty database
        var initializer = new RunDbInitializer(_factory);
        await initializer.EnsureCreatedAsync();

        // Assert: New schema tables exist with correct columns
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();

        // memory_nodes has uuid column, no content column
        var nodeCols = (await conn.QueryAsync<string>("SELECT name FROM pragma_table_info('memory_nodes')")).ToList();
        Assert.Contains("uuid", nodeCols);
        Assert.Contains("node_type", nodeCols);
        Assert.Contains("display_name", nodeCols);
        Assert.DoesNotContain("content", nodeCols);

        // memory_contents table exists
        var contentCols = (await conn.QueryAsync<string>("SELECT name FROM pragma_table_info('memory_contents')")).ToList();
        Assert.Contains("node_uuid", contentCols);
        Assert.Contains("content", contentCols);

        // memory_edges has parent_uuid, not from_uri
        var edgeCols = (await conn.QueryAsync<string>("SELECT name FROM pragma_table_info('memory_edges')")).ToList();
        Assert.Contains("parent_uuid", edgeCols);
        Assert.Contains("child_uuid", edgeCols);
        Assert.DoesNotContain("from_uri", edgeCols);

        // memory_uri_paths table exists
        var pathCols = (await conn.QueryAsync<string>("SELECT name FROM pragma_table_info('memory_uri_paths')")).ToList();
        Assert.Contains("uri", pathCols);
        Assert.Contains("node_uuid", pathCols);
    }
}
