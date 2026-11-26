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

public sealed class UserRepository(IDbConnectionFactory connectionFactory)
    : RepositoryBase(connectionFactory), IUserRepository
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
        using var conn = ConnectionFactory.CreateConnection();
        var user = await conn.QueryFirstOrDefaultAsync<UserDb>(new CommandDefinition(
            "SELECT PasswordHash, Salt, Role FROM Users WHERE Username = @Username COLLATE NOCASE",
            new { Username = username },
            cancellationToken: cancellationToken));

        if (user == null)
        {
            // Timing attack mitigation: compute dummy PBKDF2 hash to equalize execution time
            HashV4(password, "dHVta2FsaWJhc2FsdDEyMzQ1Ng==");
            return (false, "");
        }

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

        using var conn = ConnectionFactory.CreateConnection();
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
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 600000, HashAlgorithmName.SHA512, HashSize);
        return "v4:" + Convert.ToBase64String(hash);
    }

    private string HashV3(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 120000, HashAlgorithmName.SHA512, HashSize);
        return "v3:" + Convert.ToBase64String(hash);
    }

    private string HashV2Legacy(string password, string salt)
    {
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, Encoding.UTF8.GetBytes(salt), 20000, HashAlgorithmName.SHA512, HashSize);
        return "v2:" + Convert.ToBase64String(hash);
    }

    private async Task UpgradeAsync(string user, string pass, SqliteConnection conn, CancellationToken cancellationToken = default)
    {
        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        string hash = HashV4(pass, salt);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE Users SET PasswordHash=@Hash, Salt=@Salt WHERE Username=@Username COLLATE NOCASE",
            new { Hash = hash, Salt = salt, Username = user.Trim() },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ResetPasswordAsync(string username, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            if (ValidatePasswordPolicy(newPassword, username) != null) return false;

            using var conn = ConnectionFactory.CreateConnection();
            var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Users WHERE Username = @Username COLLATE NOCASE",
                new { Username = username },
                cancellationToken: cancellationToken));

            if (count == 0) return false;

            await UpgradeAsync(username, newPassword, conn, cancellationToken);
            LogAudit("Update", "Users", 0, $"Password reset for user: {username}");
            return true;
        }
        catch (Exception ex) { AppLogger.LogError("ResetPasswordAsync failed", ex); return false; }
    }

    public async Task<List<Models.User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var result = await conn.QueryAsync<Models.User>(new CommandDefinition(
            "SELECT Id, Username, Role FROM Users ORDER BY Username ASC",
            cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<bool> AddUserAsync(string username, string password, string role, CancellationToken cancellationToken = default)
    {
        try
        {
            string cleanUsername = username?.Trim() ?? "";
            string cleanRole = string.IsNullOrWhiteSpace(role) ? "user" : role.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(cleanUsername)) return false;
            if (ValidatePasswordPolicy(password, cleanUsername) != null) return false;

            using var conn = ConnectionFactory.CreateConnection();
            using var trans = await conn.BeginTransactionAsync(cancellationToken);
            var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Users WHERE Username = @Username COLLATE NOCASE",
                new { Username = cleanUsername },
                transaction: trans,
                cancellationToken: cancellationToken));
            if (count > 0) return false;

            string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            string hash = HashV4(password, salt);

            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO Users (Username, PasswordHash, Salt, Role) VALUES (@Username, @PasswordHash, @Salt, @Role)",
                new { Username = cleanUsername, PasswordHash = hash, Salt = salt, Role = cleanRole },
                transaction: trans,
                cancellationToken: cancellationToken));
            LogAudit(conn, (SqliteTransaction)trans, "Insert", "Users", 0, $"Created user: {cleanUsername} ({cleanRole})");
            await trans.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) { AppLogger.LogError("AddUserAsync failed", ex); return false; }
    }

    public async Task<bool> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = ConnectionFactory.CreateConnection();
            using var trans = await conn.BeginTransactionAsync(cancellationToken);

            var targetUser = await conn.QueryFirstOrDefaultAsync<UserDb>(new CommandDefinition(
                "SELECT Role FROM Users WHERE Id = @Id", new { Id = id }, transaction: trans, cancellationToken: cancellationToken));
            if (targetUser == null) return false;

            if (targetUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                var adminCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM Users WHERE Role = 'admin' COLLATE NOCASE", transaction: trans, cancellationToken: cancellationToken));
                if (adminCount <= 1)
                {
                    AppLogger.LogError($"Cannot delete user ID {id}: last remaining administrator.");
                    return false;
                }
            }

            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM Users WHERE Id = @Id",
                new { Id = id },
                transaction: trans,
                cancellationToken: cancellationToken));
            LogAudit(conn, (SqliteTransaction)trans, "Delete", "Users", id, $"Deleted user ID: {id}");
            await trans.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) { AppLogger.LogError("DeleteUserAsync failed", ex); return false; }
    }

    public async Task<bool> UpdateUserRoleAsync(int id, string role, CancellationToken cancellationToken = default)
    {
        try
        {
            string cleanRole = string.IsNullOrWhiteSpace(role) ? "user" : role.Trim().ToLowerInvariant();
            using var conn = ConnectionFactory.CreateConnection();
            using var trans = await conn.BeginTransactionAsync(cancellationToken);

            var targetUser = await conn.QueryFirstOrDefaultAsync<UserDb>(new CommandDefinition(
                "SELECT Role FROM Users WHERE Id = @Id", new { Id = id }, transaction: trans, cancellationToken: cancellationToken));
            if (targetUser == null) return false;

            if (targetUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase) && !cleanRole.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                var adminCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM Users WHERE Role = 'admin' COLLATE NOCASE", transaction: trans, cancellationToken: cancellationToken));
                if (adminCount <= 1)
                {
                    AppLogger.LogError($"Cannot demote user ID {id}: last remaining administrator.");
                    return false;
                }
            }

            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE Users SET Role = @Role WHERE Id = @Id",
                new { Role = cleanRole, Id = id },
                transaction: trans,
                cancellationToken: cancellationToken));
            LogAudit(conn, (SqliteTransaction)trans, "Update", "Users", id, $"Updated role for user ID: {id} to {cleanRole}");
            await trans.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) { AppLogger.LogError("UpdateUserRoleAsync failed", ex); return false; }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a == null || b == null) return false;
        byte[] aBytes = Encoding.UTF8.GetBytes(a.Trim());
        byte[] bBytes = Encoding.UTF8.GetBytes(b.Trim());

        if (aBytes.Length != bBytes.Length)
        {
            CryptographicOperations.FixedTimeEquals(aBytes, aBytes);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
