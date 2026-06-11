using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;

namespace Stokendra.Data;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.NormalizeDatabasePath(dbPath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            ForeignKeys = true,
            DefaultTimeout = 15
        };
        _connectionString = builder.ToString();
    }

    public string ConnectionString => _connectionString;

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        SqliteHelpers.RegisterCustomFunctions(connection);
        
        // Optimize SQLite performance pragmas
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA busy_timeout = 30000;
            PRAGMA foreign_keys = ON;
            PRAGMA cache_size = -32000; -- ~32MB
            PRAGMA temp_store = MEMORY;
        ";
        cmd.ExecuteNonQuery();
        
        return connection;
    }
}
