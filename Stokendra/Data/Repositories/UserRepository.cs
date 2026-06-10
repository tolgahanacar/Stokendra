using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;

namespace Stokendra.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private const int HashSize = 32;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<(bool success, string role)> VerifyPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash, Salt, Role FROM Users WHERE Username = $u COLLATE NOCASE";
        cmd.Parameters.AddWithValue("$u", username);
        
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return (false, "");
        
        string storedHash = reader.GetString(0).Trim();
        string salt = reader.GetString(1).Trim();
        string role = reader.IsDBNull(2) ? "admin" : reader.GetString(2);

        bool match = Verify(password, salt, storedHash, out bool upgrade);
        
        if (match && upgrade)
            Upgrade(username, password, conn);

        return (match, role);
    }

    public bool Authenticate(string username, string password)
    {
        var task = VerifyPasswordAsync(username, password);
        return task.GetAwaiter().GetResult().success;
    }

    public bool ChangePassword(string username, string oldPassword, string newPassword)
    {
        if (!Authenticate(username, oldPassword)) return false;
        if (ValidatePasswordPolicy(newPassword, username) != null) return false;
        
        using var conn = _connectionFactory.CreateConnection();
        Upgrade(username, newPassword, conn);
        return true;
    }

    public bool IsDefaultAdminPasswordInUse()
    {
        return Authenticate("admin", "admin");
    }

    public string? ValidatePasswordPolicy(string password, string? username = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 4) 
            return LocalizationManager.L("password_min_length");
        if (username != null && password.Contains(username, StringComparison.OrdinalIgnoreCase)) 
            return LocalizationManager.L("password_contains_username");
        return null;
    }

    private bool Verify(string password, string salt, string stored, out bool upgrade)
    {
        upgrade = !stored.StartsWith("v4:");

        if (stored.StartsWith("v4:"))
            return FixedTimeEquals(stored, HashV4(password, salt));
        
        if (stored.StartsWith("v3:"))
        {
            bool match = FixedTimeEquals(stored, HashV3(password, salt));
            upgrade = match;
            return match;
        }

        if (stored.StartsWith("v2:"))
        {
            bool match = FixedTimeEquals(stored, HashV2Legacy(password, salt));
            upgrade = match;
            return match;
        }

        // Fallback to SHA256 (very old)
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        string legacyHash = Convert.ToBase64String(bytes);
        bool legacyMatch = FixedTimeEquals(legacyHash, stored);
        upgrade = legacyMatch;
        return legacyMatch;
    }

    private string HashV4(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 600000, HashAlgorithmName.SHA512);
        return "v4:" + Convert.ToBase64String(pbkdf2.GetBytes(HashSize));
    }

    private string HashV3(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 120000, HashAlgorithmName.SHA512);
        return "v3:" + Convert.ToBase64String(pbkdf2.GetBytes(HashSize));
    }

    private string HashV2Legacy(string password, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(salt), 20000, HashAlgorithmName.SHA512);
        return "v2:" + Convert.ToBase64String(pbkdf2.GetBytes(HashSize));
    }

    private void Upgrade(string user, string pass, SqliteConnection conn)
    {
        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        string hash = HashV4(pass, salt);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET PasswordHash=$h, Salt=$s WHERE Username=$u";
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$s", salt);
        cmd.Parameters.AddWithValue("$u", user);
        cmd.ExecuteNonQuery();
    }

    public async Task<bool> ResetPasswordAsync(string username, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            // Check if user exists
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = $u COLLATE NOCASE";
            checkCmd.Parameters.AddWithValue("$u", username);
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken));
            
            if (count == 0) return false;

            Upgrade(username, newPassword, conn);
            return true;
        }
        catch { return false; }
    }

    public async Task<System.Collections.Generic.List<Models.User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var list = new System.Collections.Generic.List<Models.User>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Username, Role FROM Users ORDER BY Username ASC";
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Models.User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Role = reader.IsDBNull(2) ? "admin" : reader.GetString(2)
            });
        }
        return list;
    }

    public async Task<bool> AddUserAsync(string username, string password, string role, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = $u COLLATE NOCASE";
            checkCmd.Parameters.AddWithValue("$u", username.Trim());
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (count > 0) return false;

            string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            string hash = HashV4(password, salt);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Users (Username, PasswordHash, Salt, Role) VALUES ($u, $h, $s, $r)";
            cmd.Parameters.AddWithValue("$u", username.Trim());
            cmd.Parameters.AddWithValue("$h", hash);
            cmd.Parameters.AddWithValue("$s", salt);
            cmd.Parameters.AddWithValue("$r", role.Trim());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Users WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> UpdateUserRoleAsync(int id, string role, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET Role = $r WHERE Id = $id";
            cmd.Parameters.AddWithValue("$r", role.Trim());
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch { return false; }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a == null || b == null) return false;
        byte[] aBytes = Encoding.UTF8.GetBytes(a.Trim());
        byte[] bBytes = Encoding.UTF8.GetBytes(b.Trim());
        return aBytes.Length == bBytes.Length &&
               CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
