using Dapper;
using Microsoft.Extensions.Logging;

namespace OpenAnima.Core.ChatPersistence;

/// <summary>
/// Creates the SQLite schema for the durable chat database.
/// Must be called before any chat persistence operations are performed.
/// Safe to call multiple times — all statements use <c>IF NOT EXISTS</c> and are idempotent.
/// </summary>
public class ChatDbInitializer
{
    private readonly ChatDbConnectionFactory _factory;
    private readonly ILogger<ChatDbInitializer> _logger;

    /// <summary>The complete schema creation script, executed as a single batch.</summary>
    private const string SchemaScript = """
        CREATE TABLE IF NOT EXISTS chat_messages (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            anima_id TEXT NOT NULL,
            role TEXT NOT NULL,
            content TEXT NOT NULL,
            tool_calls_json TEXT,
            input_tokens INTEGER DEFAULT 0,
            output_tokens INTEGER DEFAULT 0,
            created_at DATETIME DEFAULT CURRENT_TIMESTAMP
        );

        CREATE INDEX IF NOT EXISTS idx_chat_messages_anima_id ON chat_messages(anima_id);
        """;

    /// <summary>
    /// Initializes a new <see cref="ChatDbInitializer"/> using the provided connection factory.
    /// </summary>
    /// <param name="factory">The factory used to obtain a <see cref="Microsoft.Data.Sqlite.SqliteConnection"/>.</param>
    /// <param name="logger">Logger for migration diagnostics.</param>
    public ChatDbInitializer(ChatDbConnectionFactory factory, ILogger<ChatDbInitializer> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ensures the database schema exists. Creates the chat_messages table and indexes if they do not already exist.
    /// This method is idempotent and safe to call on every application startup.
    /// </summary>
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        // Create table and indexes. All statements use IF NOT EXISTS so this is idempotent.
        await conn.ExecuteAsync(SchemaScript);

        _logger.LogInformation("Chat database initialized");
    }
}
