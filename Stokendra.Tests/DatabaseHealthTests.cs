using Xunit;
using Stokendra.Data;
using System.IO;
using System;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class DatabaseHealthTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly Database _database;

    public DatabaseHealthTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_Test_DBHealth_{Guid.NewGuid():N}.db");
        _database = new Database(_tempDbPath);
    }

    public void Dispose()
    {
        _database.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public void Test_Database_CreatesFile_And_AppliesMigrations()
    {
        // Assert file exists after initialization
        Assert.True(File.Exists(_tempDbPath));
        
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_tempDbPath}");
        conn.Open();
        // Check if Users table exists
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users';";
        var result = cmd.ExecuteScalar();
        Assert.Equal("Users", result);

        // Check if ServiceRecords table exists
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ServiceRecords';";
        result = cmd.ExecuteScalar();
        Assert.Equal("ServiceRecords", result);

        // Check user_version to ensure migrations ran (Current version in code is 18 typically, but > 0 is fine)
        cmd.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.True(version > 0, "Migrations should have updated user_version");
    }

    [Fact]
    public void Test_Database_DefaultAdminCreated()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_tempDbPath}");
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = 'admin' AND Role = 'admin'";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.True(count > 0, "Default admin user must be created");
    }
}
