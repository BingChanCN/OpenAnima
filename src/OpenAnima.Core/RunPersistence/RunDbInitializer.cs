using Dapper;

namespace OpenAnima.Core.RunPersistence;

/// <summary>
/// Creates the SQLite schema for the durable run database.
/// Must be called before any <see cref="RunRepository"/> operations are performed.
/// Safe to call multiple times — all statements use <c>IF NOT EXISTS</c> and are idempotent.
/// </summary>
public class RunDbInitializer
{
    private readonly RunDbConnectionFactory _factory;

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
        """;

    /// <summary>
    /// Initializes a new <see cref="RunDbInitializer"/> using the provided connection factory.
    /// </summary>
    /// <param name="factory">The factory used to obtain a <see cref="Microsoft.Data.Sqlite.SqliteConnection"/>.</param>
    public RunDbInitializer(RunDbConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Ensures the database schema exists. Creates all tables and indexes if they do not already exist.
    /// Enables WAL journal mode and sets synchronous mode to NORMAL for safe concurrent access.
    /// This method is idempotent and safe to call on every application startup.
    /// </summary>
    public async Task EnsureCreatedAsync()
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();

        // WAL mode enables concurrent reads while writing — required for Phase 47 UI queries
        await conn.ExecuteAsync("PRAGMA journal_mode=WAL;");
        await conn.ExecuteAsync("PRAGMA synchronous=NORMAL;");
        await conn.ExecuteAsync(SchemaScript);
    }
}
