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

    [Fact]
    public void Test_SqlLike_OptimizedPatterns_MatchTurkishAccurate()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_tempDbPath}");
        conn.Open();
        SqliteHelpers.RegisterCustomFunctions(conn);

        using var cmd = conn.CreateCommand();

        // 1. Exact match
        cmd.CommandText = "SELECT LIKE('ELMA', 'elma');";
        Assert.Equal(1L, cmd.ExecuteScalar());

        // 2. StartsWith (prefix%)
        cmd.CommandText = "SELECT LIKE('EL%', 'elma');";
        Assert.Equal(1L, cmd.ExecuteScalar());
        cmd.CommandText = "SELECT LIKE('ARM%', 'elma');";
        Assert.Equal(0L, cmd.ExecuteScalar());

        // 3. EndsWith (%suffix)
        cmd.CommandText = "SELECT LIKE('%MA', 'elma');";
        Assert.Equal(1L, cmd.ExecuteScalar());
        cmd.CommandText = "SELECT LIKE('%Z', 'elma');";
        Assert.Equal(0L, cmd.ExecuteScalar());

        // 4. Contains (%term%) with Turkish dotted/dotless I
        cmd.CommandText = "SELECT LIKE('%İĞNE%', 'iğne ipliği');";
        Assert.Equal(1L, cmd.ExecuteScalar());
        cmd.CommandText = "SELECT LIKE('%IŞIK%', 'ışık kaynağı');";
        Assert.Equal(1L, cmd.ExecuteScalar());
    }

    [Fact]
    public async Task Test_LastAdmin_CannotBeDeleted()
    {
        var factory = new SqliteConnectionFactory(_tempDbPath);
        var repo = new Stokendra.Data.Repositories.UserRepository(factory);

        var users = await repo.GetUsersAsync();
        var admin = users.FirstOrDefault(u => u.Role == "admin");
        Assert.NotNull(admin);

        // Attempting to delete the last admin should fail
        bool result = await repo.DeleteUserAsync(admin.Id);
        Assert.False(result, "Deleting the last remaining admin must be prohibited");

        // Verify admin still exists
        var usersAfter = await repo.GetUsersAsync();
        Assert.Contains(usersAfter, u => u.Id == admin.Id);
    }

    [Fact]
    public async Task Test_ResetPassword_ValidatesPolicy()
    {
        var factory = new SqliteConnectionFactory(_tempDbPath);
        var repo = new Stokendra.Data.Repositories.UserRepository(factory);

        // Weak password (< 4 chars) should fail policy validation
        bool resultShort = await repo.ResetPasswordAsync("admin", "12");
        Assert.False(resultShort);

        // Password containing username should fail policy validation
        bool resultUsername = await repo.ResetPasswordAsync("admin", "admin123");
        Assert.False(resultUsername);

        // Valid password should succeed
        bool resultValid = await repo.ResetPasswordAsync("admin", "SecretPass123!");
        Assert.True(resultValid);
    }

    [Fact]
    public void Test_PrintTemplateBuilder_EscapesHtml()
    {
        string malicious = "<script>alert('xss')</script>";
        string escaped = Stokendra.Infrastructure.PrintTemplateBuilder.Escape(malicious);
        Assert.DoesNotContain("<script>", escaped);
        Assert.Contains("&lt;script&gt;", escaped);
    }
}
