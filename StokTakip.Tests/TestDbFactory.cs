using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;

namespace StokTakip.Tests;

public class TestDbFactory : IDbConnectionFactory
{
    private readonly string _path;
    public TestDbFactory(string path) => _path = path;

    public string ConnectionString => $"Data Source={_path}";

    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }
}
