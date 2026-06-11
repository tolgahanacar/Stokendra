using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;

namespace Stokendra.Data.Repositories;

public sealed class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    private const int HashSize = 32;

    private class UserDb
    {
        public string PasswordHash { get; set; } = "";
        public string Salt { get; set; } = "";
        public string Role { get; set; } = "admin";
    }

    public async Task<(bool success, string role)> VerifyPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using var conn = connectionFactory.CreateConnection();
        var user = await conn.QueryFirstOrDefaultAsync<UserDb>(new CommandDefinition(
            "SELECT PasswordHash, Salt, Role FROM Users WHERE Username = @Username COLLATE NOCASE",
            new { Username = username },
            cancellationToken: cancellationToken));

        if (user == null)
            return (false, "");

        string storedHash = user.PasswordHash.Trim();
        string salt = user.Salt.Trim();
        string role = user.Role;

        bool match = Verify(password, salt, storedHash, out bool upgrade);
        
        if (match && upgrade)
            await UpgradeAsync(username, password, conn, cancellationToken);

        return (match, role);
    }

    public async Task<bool> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var res = await VerifyPasswordAsync(username, password, cancellationToken);
        return res.success;
    }

    public async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!await AuthenticateAsync(username, oldPassword, cancellationToken)) return false;
        if (ValidatePasswordPolicy(newPassword, username) != null) return false;
        
        using var conn = connectionFactory.CreateConnection();
        await UpgradeAsync(username, newPassword, conn, cancellationToken);
        return true;
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

    private async Task UpgradeAsync(string user, string pass, SqliteConnection conn, CancellationToken cancellationToken = default)
    {
        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        string hash = HashV4(pass, salt);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE Users SET PasswordHash=@Hash, Salt=@Salt WHERE Username=@Username",
            new { Hash = hash, Salt = salt, Username = user },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ResetPasswordAsync(string username, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = connectionFactory.CreateConnection();
            var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Users WHERE Username = @Username COLLATE NOCASE",
                new { Username = username },
                cancellationToken: cancellationToken));
            
            if (count == 0) return false;

            await UpgradeAsync(username, newPassword, conn, cancellationToken);
            return true;
        }
        catch { return false; }
    }

    public async Task<List<Models.User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var conn = connectionFactory.CreateConnection();
        var result = await conn.QueryAsync<Models.User>(new CommandDefinition(
            "SELECT Id, Username, Role FROM Users ORDER BY Username ASC",
            cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<bool> AddUserAsync(string username, string password, string role, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = connectionFactory.CreateConnection();
            var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Users WHERE Username = @Username COLLATE NOCASE",
                new { Username = username.Trim() },
                cancellationToken: cancellationToken));
            if (count > 0) return false;

            string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            string hash = HashV4(password, salt);
            
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO Users (Username, PasswordHash, Salt, Role) VALUES (@Username, @PasswordHash, @Salt, @Role)",
                new { Username = username.Trim(), PasswordHash = hash, Salt = salt, Role = role.Trim() },
                cancellationToken: cancellationToken));
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = connectionFactory.CreateConnection();
            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM Users WHERE Id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> UpdateUserRoleAsync(int id, string role, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = connectionFactory.CreateConnection();
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE Users SET Role = @Role WHERE Id = @Id",
                new { Role = role.Trim(), Id = id },
                cancellationToken: cancellationToken));
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
