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
    private const int BusyTimeoutSeconds = 5;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a production factory that connects to a SQLite file at the given path.
    /// The connection string is built with a five-second default timeout.
    /// </summary>
    /// <param name="dbPath">Absolute or relative path to the SQLite database file.</param>
    public ChatDbConnectionFactory(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            DefaultTimeout = BusyTimeoutSeconds
        };
        _connectionString = builder.ToString();
    }

    /// <summary>
    /// Initializes a factory using a raw connection string.
    /// Use this constructor for in-memory testing with a connection string such as
    /// <c>Data Source=:memory:;Mode=Memory;Cache=Shared</c>.
    /// </summary>
    /// <param name="connectionString">The full SQLite connection string.</param>
    /// <param name="isRaw">Pass <c>true</c> to use the connection string as-is without modification.</param>
    public ChatDbConnectionFactory(string connectionString, bool isRaw)
    {
        _ = isRaw; // distinguishes overloads
        _connectionString = connectionString;
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
