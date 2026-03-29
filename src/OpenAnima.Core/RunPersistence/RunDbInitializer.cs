using Dapper;
using Microsoft.Extensions.Logging;

namespace OpenAnima.Core.RunPersistence;

/// <summary>
/// Creates the SQLite schema for the durable run database.
/// Must be called before any <see cref="RunRepository"/> operations are performed.
/// Safe to call multiple times — all statements use <c>IF NOT EXISTS</c> and are idempotent.
/// </summary>
public class RunDbInitializer
{
    private readonly RunDbConnectionFactory _factory;
    private readonly ILogger<RunDbInitializer>? _logger;

    /// <summary>The complete schema creation script, executed as a single batch.</summary>
    private const string SchemaScript = """
        CREATE TABLE IF NOT EXISTS runs (
            run_id          TEXT NOT NULL PRIMARY KEY,
            anima_id        TEXT NOT NULL,
            objective       TEXT NOT NULL,
            workspace_root  TEXT NOT NULL,
            max_steps       INTEGER,
            max_wall_seconds INTEGER,
            created_at      TEXT NOT NULL,
            updated_at      TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS run_state_events (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            run_id          TEXT NOT NULL,
            state           TEXT NOT NULL,
            reason          TEXT,
            occurred_at     TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS step_events (
            step_id         TEXT NOT NULL PRIMARY KEY,
            run_id          TEXT NOT NULL,
            propagation_id  TEXT NOT NULL,
            module_name     TEXT NOT NULL,
            status          TEXT NOT NULL,
            input_summary   TEXT,
            output_summary  TEXT,
            artifact_ref_id TEXT,
            error_info      TEXT,
            duration_ms     INTEGER,
            occurred_at     TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_run_state_events_run_id ON run_state_events(run_id, occurred_at);
        CREATE INDEX IF NOT EXISTS idx_step_events_run_id ON step_events(run_id, occurred_at);
        CREATE INDEX IF NOT EXISTS idx_step_events_propagation ON step_events(propagation_id);

        CREATE TABLE IF NOT EXISTS artifacts (
            artifact_id     TEXT NOT NULL PRIMARY KEY,
            run_id          TEXT NOT NULL,
            step_id         TEXT NOT NULL,
            mime_type       TEXT NOT NULL,
            file_path       TEXT NOT NULL,
            file_size_bytes INTEGER NOT NULL,
            created_at      TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_artifacts_run_id ON artifacts(run_id);
        CREATE INDEX IF NOT EXISTS idx_artifacts_step_id ON artifacts(step_id);

        CREATE TABLE IF NOT EXISTS memory_nodes (
            uuid        TEXT NOT NULL PRIMARY KEY,
            anima_id    TEXT NOT NULL,
            node_type   TEXT NOT NULL DEFAULT 'Fact',
            display_name TEXT,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS memory_contents (
            id                  INTEGER PRIMARY KEY AUTOINCREMENT,
            node_uuid           TEXT NOT NULL,
            anima_id            TEXT NOT NULL,
            content             TEXT NOT NULL,
            disclosure_trigger  TEXT,
            keywords            TEXT,
            source_artifact_id  TEXT,
            source_step_id      TEXT,
            created_at          TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS memory_edges (
            id                  INTEGER PRIMARY KEY AUTOINCREMENT,
            anima_id            TEXT NOT NULL,
            parent_uuid         TEXT NOT NULL,
            child_uuid          TEXT NOT NULL,
            label               TEXT NOT NULL,
            priority            INTEGER NOT NULL DEFAULT 0,
            weight              REAL NOT NULL DEFAULT 1.0,
            bidirectional       INTEGER NOT NULL DEFAULT 0,
            disclosure_trigger  TEXT,
            created_at          TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS memory_uri_paths (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            uri         TEXT NOT NULL,
            node_uuid   TEXT NOT NULL,
            anima_id    TEXT NOT NULL,
            created_at  TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_memory_nodes_anima ON memory_nodes(anima_id);
        CREATE INDEX IF NOT EXISTS idx_memory_contents_node ON memory_contents(node_uuid, id DESC);
        CREATE INDEX IF NOT EXISTS idx_memory_contents_anima ON memory_contents(anima_id);
        CREATE INDEX IF NOT EXISTS idx_memory_edges_anima ON memory_edges(anima_id, parent_uuid);
        CREATE INDEX IF NOT EXISTS idx_memory_edges_child ON memory_edges(anima_id, child_uuid);
        CREATE UNIQUE INDEX IF NOT EXISTS idx_memory_uri_paths_uri_anima ON memory_uri_paths(uri, anima_id);
        CREATE INDEX IF NOT EXISTS idx_memory_uri_paths_node ON memory_uri_paths(node_uuid);
        """;

    /// <summary>
    /// Initializes a new <see cref="RunDbInitializer"/> using the provided connection factory.
    /// </summary>
    /// <param name="factory">The factory used to obtain a <see cref="Microsoft.Data.Sqlite.SqliteConnection"/>.</param>
    /// <param name="logger">Optional logger for migration diagnostics.</param>
    public RunDbInitializer(RunDbConnectionFactory factory, ILogger<RunDbInitializer>? logger = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger;
    }

    /// <summary>
    /// Ensures the database schema exists. Creates all tables and indexes if they do not already exist.
    /// Enables WAL journal mode and sets synchronous mode to NORMAL for safe concurrent access.
    /// This method is idempotent and safe to call on every application startup.
    /// </summary>
    /// <remarks>
    /// Migration order matters: <see cref="MigrateToFourTableModelAsync"/> must run before
    /// <see cref="SchemaScript"/> so that the new memory table indexes (referencing parent_uuid etc.)
    /// are only applied after the old tables have been dropped and recreated with the new schema.
    /// </remarks>
    public async Task EnsureCreatedAsync()
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();

        // WAL mode enables concurrent reads while writing -- required for Phase 47 UI queries
        await conn.ExecuteAsync("PRAGMA journal_mode=WAL;");
        await conn.ExecuteAsync("PRAGMA synchronous=NORMAL;");

        // Phase 65: migrate old 3-table memory model BEFORE running SchemaScript.
        // SchemaScript uses CREATE INDEX ... ON memory_edges(anima_id, parent_uuid) which would
        // fail against the old table (which has from_uri/to_uri, not parent_uuid). Running the
        // migration first drops and recreates the memory tables so SchemaScript indexes succeed.
        await MigrateToFourTableModelAsync(conn);

        // Create all tables and indexes. All statements use IF NOT EXISTS so this is idempotent.
        await conn.ExecuteAsync(SchemaScript);

        // Additive column migrations for rows added after initial schema creation
        await MigrateSchemaAsync(conn);
    }

    /// <summary>
    /// Applies additive schema migrations for columns added after the initial schema creation.
    /// All migrations are idempotent -- safe to call on every startup.
    /// </summary>
    private async Task MigrateSchemaAsync(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        // Check if workflow_preset column exists (added in Phase 49-02)
        var columns = await conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('runs')");
        var columnSet = new HashSet<string>(columns);

        if (!columnSet.Contains("workflow_preset"))
        {
            await conn.ExecuteAsync("ALTER TABLE runs ADD COLUMN workflow_preset TEXT");
        }

        // Check if deprecated column exists on memory_nodes (added in Phase 67)
        var memoryNodeColumns = await conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('memory_nodes')");
        var memoryNodeColSet = new HashSet<string>(memoryNodeColumns);

        if (!memoryNodeColSet.Contains("deprecated"))
        {
            await conn.ExecuteAsync("ALTER TABLE memory_nodes ADD COLUMN deprecated INTEGER NOT NULL DEFAULT 0");
            await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_memory_nodes_deprecated ON memory_nodes(anima_id, deprecated)");
        }
    }

    /// <summary>
    /// Migrates the old 3-table memory model (memory_nodes with content column,
    /// memory_edges with from_uri/to_uri, memory_snapshots) to the new 4-table model
    /// (memory_nodes with UUID PK, memory_contents, memory_edges with parent_uuid/child_uuid,
    /// memory_uri_paths). Runs inside an atomic transaction with automatic backup.
    /// </summary>
    private async Task MigrateToFourTableModelAsync(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        // Detect old schema: old memory_nodes has a 'content' column; new one does not.
        var nodeColumns = await conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('memory_nodes')");
        var nodeColSet = new HashSet<string>(nodeColumns);

        if (!nodeColSet.Contains("content"))
            return; // Already migrated or fresh install with new schema

        _logger?.LogInformation("Detected old memory schema. Starting migration to four-table model...");

        // Backup the database file before migration
        var dbPath = conn.DataSource;
        if (!string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath))
        {
            var backupPath = $"{dbPath}.bak-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            System.IO.File.Copy(dbPath, backupPath);
            _logger?.LogInformation("Database backed up to {BackupPath}", backupPath);
        }

        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            // 1. Read all old data before dropping tables.
            //    Use explicit AS aliases to guarantee Dapper property mapping
            //    (avoids reliance on underscore-stripping convention).
            var oldNodes = (await conn.QueryAsync<OldMemoryNode>(
                @"SELECT uri AS Uri, anima_id AS AnimaId, content AS Content,
                         disclosure_trigger AS DisclosureTrigger, keywords AS Keywords,
                         source_artifact_id AS SourceArtifactId, source_step_id AS SourceStepId,
                         created_at AS CreatedAt, updated_at AS UpdatedAt
                  FROM memory_nodes",
                transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx)).ToList();

            var oldEdges = (await conn.QueryAsync<OldMemoryEdge>(
                @"SELECT id AS Id, anima_id AS AnimaId, from_uri AS FromUri,
                         to_uri AS ToUri, label AS Label, created_at AS CreatedAt
                  FROM memory_edges",
                transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx)).ToList();

            var oldSnapshots = (await conn.QueryAsync<OldMemorySnapshot>(
                @"SELECT id AS Id, uri AS Uri, anima_id AS AnimaId,
                         content AS Content, snapshot_at AS SnapshotAt
                  FROM memory_snapshots",
                transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx)).ToList();

            _logger?.LogInformation("Read {NodeCount} nodes, {EdgeCount} edges, {SnapshotCount} snapshots from old schema",
                oldNodes.Count, oldEdges.Count, oldSnapshots.Count);

            // 2. Build UUID map: (uri, anima_id) -> uuid
            var uuidMap = new Dictionary<(string Uri, string AnimaId), string>();
            foreach (var node in oldNodes)
            {
                uuidMap[(node.Uri, node.AnimaId)] = Guid.NewGuid().ToString("D");
            }

            // 3. Drop old tables and indexes
            await conn.ExecuteAsync("DROP INDEX IF EXISTS idx_memory_nodes_anima", transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
            await conn.ExecuteAsync("DROP INDEX IF EXISTS idx_memory_edges_anima", transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
            await conn.ExecuteAsync("DROP INDEX IF EXISTS idx_memory_edges_to_uri", transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
            await conn.ExecuteAsync("DROP INDEX IF EXISTS idx_memory_snapshots_uri", transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
            await conn.ExecuteAsync("DROP TABLE IF EXISTS memory_snapshots", transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
            await conn.ExecuteAsync("DROP TABLE IF EXISTS memory_edges", transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
            await conn.ExecuteAsync("DROP TABLE IF EXISTS memory_nodes", transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);

            // 4. Create new tables (same structure as SchemaScript memory section)
            await conn.ExecuteAsync("""
                CREATE TABLE memory_nodes (
                    uuid        TEXT NOT NULL PRIMARY KEY,
                    anima_id    TEXT NOT NULL,
                    node_type   TEXT NOT NULL DEFAULT 'Fact',
                    display_name TEXT,
                    created_at  TEXT NOT NULL,
                    updated_at  TEXT NOT NULL
                );

                CREATE TABLE memory_contents (
                    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                    node_uuid           TEXT NOT NULL,
                    anima_id            TEXT NOT NULL,
                    content             TEXT NOT NULL,
                    disclosure_trigger  TEXT,
                    keywords            TEXT,
                    source_artifact_id  TEXT,
                    source_step_id      TEXT,
                    created_at          TEXT NOT NULL
                );

                CREATE TABLE memory_edges (
                    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                    anima_id            TEXT NOT NULL,
                    parent_uuid         TEXT NOT NULL,
                    child_uuid          TEXT NOT NULL,
                    label               TEXT NOT NULL,
                    priority            INTEGER NOT NULL DEFAULT 0,
                    weight              REAL NOT NULL DEFAULT 1.0,
                    bidirectional       INTEGER NOT NULL DEFAULT 0,
                    disclosure_trigger  TEXT,
                    created_at          TEXT NOT NULL
                );

                CREATE TABLE memory_uri_paths (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    uri         TEXT NOT NULL,
                    node_uuid   TEXT NOT NULL,
                    anima_id    TEXT NOT NULL,
                    created_at  TEXT NOT NULL
                );

                CREATE INDEX idx_memory_nodes_anima ON memory_nodes(anima_id);
                CREATE INDEX idx_memory_contents_node ON memory_contents(node_uuid, id DESC);
                CREATE INDEX idx_memory_contents_anima ON memory_contents(anima_id);
                CREATE INDEX idx_memory_edges_anima ON memory_edges(anima_id, parent_uuid);
                CREATE INDEX idx_memory_edges_child ON memory_edges(anima_id, child_uuid);
                CREATE UNIQUE INDEX idx_memory_uri_paths_uri_anima ON memory_uri_paths(uri, anima_id);
                CREATE INDEX idx_memory_uri_paths_node ON memory_uri_paths(node_uuid);
                """, transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);

            // 5. Migrate nodes -> memory_nodes + memory_uri_paths
            //    Insert content in two passes so snapshots (older versions) get lower IDs
            //    and the current content row always gets the highest ID (= latest version).
            foreach (var old in oldNodes)
            {
                var uuid = uuidMap[(old.Uri, old.AnimaId)];
                var nodeType = InferNodeType(old.Uri);
                var displayName = ExtractDisplayName(old.Uri);

                await conn.ExecuteAsync(
                    "INSERT INTO memory_nodes (uuid, anima_id, node_type, display_name, created_at, updated_at) VALUES (@Uuid, @AnimaId, @NodeType, @DisplayName, @CreatedAt, @UpdatedAt)",
                    new { Uuid = uuid, old.AnimaId, NodeType = nodeType, DisplayName = displayName, old.CreatedAt, old.UpdatedAt },
                    transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);

                // URI path entry
                await conn.ExecuteAsync(
                    "INSERT INTO memory_uri_paths (uri, node_uuid, anima_id, created_at) VALUES (@Uri, @NodeUuid, @AnimaId, @CreatedAt)",
                    new { old.Uri, NodeUuid = uuid, old.AnimaId, old.CreatedAt },
                    transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
            }

            // 6. Migrate snapshots -> older memory_contents rows (inserted first for lower IDs)
            foreach (var snap in oldSnapshots)
            {
                if (uuidMap.TryGetValue((snap.Uri, snap.AnimaId), out var uuid))
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO memory_contents (node_uuid, anima_id, content, disclosure_trigger, keywords, source_artifact_id, source_step_id, created_at) VALUES (@NodeUuid, @AnimaId, @Content, NULL, NULL, NULL, NULL, @CreatedAt)",
                        new { NodeUuid = uuid, snap.AnimaId, snap.Content, CreatedAt = snap.SnapshotAt },
                        transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
                }
            }

            // 6b. Insert current content as the latest version (highest ID)
            foreach (var old in oldNodes)
            {
                var uuid = uuidMap[(old.Uri, old.AnimaId)];

                await conn.ExecuteAsync(
                    "INSERT INTO memory_contents (node_uuid, anima_id, content, disclosure_trigger, keywords, source_artifact_id, source_step_id, created_at) VALUES (@NodeUuid, @AnimaId, @Content, @DisclosureTrigger, @Keywords, @SourceArtifactId, @SourceStepId, @CreatedAt)",
                    new { NodeUuid = uuid, old.AnimaId, old.Content, old.DisclosureTrigger, old.Keywords, old.SourceArtifactId, old.SourceStepId, CreatedAt = old.UpdatedAt },
                    transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
            }

            // 7. Migrate edges (URI-based -> UUID-based)
            foreach (var old in oldEdges)
            {
                if (uuidMap.TryGetValue((old.FromUri, old.AnimaId), out var parentUuid) &&
                    uuidMap.TryGetValue((old.ToUri, old.AnimaId), out var childUuid))
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO memory_edges (anima_id, parent_uuid, child_uuid, label, priority, weight, bidirectional, disclosure_trigger, created_at) VALUES (@AnimaId, @ParentUuid, @ChildUuid, @Label, 0, 1.0, 0, NULL, @CreatedAt)",
                        new { old.AnimaId, ParentUuid = parentUuid, ChildUuid = childUuid, old.Label, old.CreatedAt },
                        transaction: (Microsoft.Data.Sqlite.SqliteTransaction)tx);
                }
                else
                {
                    _logger?.LogWarning("Skipping edge {FromUri} -> {ToUri}: one or both nodes not found in UUID map", old.FromUri, old.ToUri);
                }
            }

            await tx.CommitAsync();
            _logger?.LogInformation("Memory schema migration complete: {NodeCount} nodes, {EdgeCount} edges, {SnapshotCount} content versions migrated",
                oldNodes.Count, oldEdges.Count, oldSnapshots.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Memory schema migration FAILED. Rolling back. Old data is safe in backup file.");
            await tx.RollbackAsync();
            throw new InvalidOperationException("Memory schema migration failed. Application cannot start with inconsistent schema. See backup file.", ex);
        }
    }

    /// <summary>Infers node type from URI prefix per CONTEXT.md mapping.</summary>
    private static string InferNodeType(string uri)
    {
        if (uri.StartsWith("core://", StringComparison.Ordinal)) return "System";
        if (uri.StartsWith("sediment://fact/", StringComparison.Ordinal)) return "Fact";
        if (uri.StartsWith("sediment://preference/", StringComparison.Ordinal)) return "Preference";
        if (uri.StartsWith("sediment://entity/", StringComparison.Ordinal)) return "Entity";
        if (uri.StartsWith("sediment://learning/", StringComparison.Ordinal)) return "Learning";
        if (uri.StartsWith("run://", StringComparison.Ordinal)) return "Artifact";
        return "Fact";
    }

    /// <summary>Extracts last segment after the final '/' as display name.</summary>
    private static string ExtractDisplayName(string uri)
    {
        var lastSlash = uri.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < uri.Length - 1
            ? uri[(lastSlash + 1)..]
            : uri;
    }

    // ---- Migration DTO types (internal only) ----

    private record OldMemoryNode
    {
        public string Uri { get; init; } = string.Empty;
        public string AnimaId { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string? DisclosureTrigger { get; init; }
        public string? Keywords { get; init; }
        public string? SourceArtifactId { get; init; }
        public string? SourceStepId { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
    }

    private record OldMemoryEdge
    {
        public int Id { get; init; }
        public string AnimaId { get; init; } = string.Empty;
        public string FromUri { get; init; } = string.Empty;
        public string ToUri { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
    }

    private record OldMemorySnapshot
    {
        public int Id { get; init; }
        public string Uri { get; init; } = string.Empty;
        public string AnimaId { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string SnapshotAt { get; init; } = string.Empty;
    }
}
