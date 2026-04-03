using Microsoft.Data.Sqlite;
using OpenAnima.Core.ChatPersistence;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Tests.Unit;

public class SqliteConnectionFactoryTests : IDisposable
{
    private readonly string _tempDirectory;

    public SqliteConnectionFactoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"openanima-sqlite-factory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void RunDbConnectionFactory_ProductionConstructor_UsesSupportedTimeoutKeyword()
    {
        var dbPath = Path.Combine(_tempDirectory, "runs.db");
        var factory = new RunDbConnectionFactory(dbPath);

        var builder = new SqliteConnectionStringBuilder(factory.ConnectionString);

        Assert.Equal(dbPath, builder.DataSource);
        Assert.Equal(5, builder.DefaultTimeout);
    }

    [Fact]
    public void ChatDbConnectionFactory_ProductionConstructor_UsesSupportedTimeoutKeyword()
    {
        var dbPath = Path.Combine(_tempDirectory, "chat.db");
        var factory = new ChatDbConnectionFactory(dbPath);

        var builder = new SqliteConnectionStringBuilder(factory.ConnectionString);

        Assert.Equal(dbPath, builder.DataSource);
        Assert.Equal(5, builder.DefaultTimeout);
    }

    [Fact]
    public void RunDbConnectionFactory_RawConstructor_PreservesOriginalConnectionString()
    {
        const string connectionString = "Data Source=RunFactoryTests;Mode=Memory;Cache=Shared";
        var factory = new RunDbConnectionFactory(connectionString, isRaw: true);

        Assert.Equal(connectionString, factory.ConnectionString);
    }

    [Fact]
    public void ChatDbConnectionFactory_RawConstructor_PreservesOriginalConnectionString()
    {
        const string connectionString = "Data Source=ChatFactoryTests;Mode=Memory;Cache=Shared";
        var factory = new ChatDbConnectionFactory(connectionString, isRaw: true);

        Assert.Equal(connectionString, factory.ConnectionString);
    }

    [Fact]
    public void RunDbConnectionFactory_CreateConnection_DoesNotThrowForProductionConnectionString()
    {
        var dbPath = Path.Combine(_tempDirectory, "runs-open.db");
        var factory = new RunDbConnectionFactory(dbPath);

        using var connection = factory.CreateConnection();

        connection.Open();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public void ChatDbConnectionFactory_CreateConnection_DoesNotThrowForProductionConnectionString()
    {
        var dbPath = Path.Combine(_tempDirectory, "chat-open.db");
        var factory = new ChatDbConnectionFactory(dbPath);

        using var connection = factory.CreateConnection();

        connection.Open();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }
}
