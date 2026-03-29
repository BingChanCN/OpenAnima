using Microsoft.Data.Sqlite;

namespace OpenAnima.Core.ChatPersistence;

/// <summary>
/// Provides <see cref="SqliteConnection"/> instances for the durable chat database.
/// Constructed once as a singleton and shared across the application. Each call to
/// <see cref="CreateConnection"/> returns a new, unopened connection — callers are responsible
/// for opening and disposing it (typically via <c>await using</c>).
/// </summary>
public class ChatDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a production factory that connects to a SQLite file at the given path.
    /// The connection string is built as <c>Data Source={dbPath};Busy Timeout=5000</c>.
    /// </summary>
    /// <param name="dbPath">Absolute or relative path to the SQLite database file.</param>
    public ChatDbConnectionFactory(string dbPath)
    {
        _connectionString = $"Data Source={dbPath};Busy Timeout=5000";
    }

    /// <summary>The connection string used to create connections.</summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Creates a new, unopened <see cref="SqliteConnection"/> using the configured connection string.
    /// The caller must open and dispose the returned connection.
    /// </summary>
    public SqliteConnection CreateConnection() =>
        new SqliteConnection(_connectionString);
}
