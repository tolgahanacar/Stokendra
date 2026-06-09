using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;

namespace Stokendra.Tests;

public class TestDbFactory : IDbConnectionFactory
{
    private readonly string _path;
    public TestDbFactory(string path) => _path = path;

    public string ConnectionString => $"Data Source={_path}";

    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        Stokendra.Data.SqliteHelpers.RegisterCustomFunctions(conn);
        return conn;
    }
}
