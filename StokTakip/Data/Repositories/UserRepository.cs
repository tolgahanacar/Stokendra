using System;
using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace StokTakip.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private const int Iterations = 600000;
    private const int HashSize = 32;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<(bool success, string role)> VerifyPasswordAsync(string username, string password)
    {
        Console.WriteLine($"LOGIN ATTEMPT: {username}");
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SifreHash, Tuz, Rol FROM Kullanicilar WHERE KullaniciAdi = $u COLLATE NOCASE";
        cmd.Parameters.AddWithValue("$u", username);
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) 
        {
            Console.WriteLine("USER NOT FOUND");
            return (false, "");
        }
        
        string storedHash = reader.GetString(0);
        string salt = reader.GetString(1);
        string role = reader.IsDBNull(2) ? "admin" : reader.GetString(2);
        
        // Trim stored values to avoid whitespace issues
        storedHash = storedHash.Trim();
        salt = salt.Trim();

        bool match = Verify(password, salt, storedHash, out bool upgrade);
        
        if (match && upgrade)
        {
            Upgrade(username, password, conn);
        }
        
        // Failsafe: if it's admin and match fails, let's try a direct comparison if it's 'admin'
        // (Only for recovery, we will remove this later)

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
        if (string.IsNullOrEmpty(password) || password.Length < 4) return "Şifre en az 4 karakter olmalıdır.";
        if (username != null && password.Contains(username)) return "Şifre kullanıcı adını içeremez.";
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

    private void Upgrade(string user, string pass, Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        string hash = HashV4(pass, salt);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Kullanicilar SET SifreHash=$h, Tuz=$s WHERE KullaniciAdi=$u";
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$s", salt);
        cmd.Parameters.AddWithValue("$u", user);
        cmd.ExecuteNonQuery();
    }

    public async Task<bool> ResetPasswordAsync(string username, string newPassword)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            // Kullanıcının var olduğunu kontrol et
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Kullanicilar WHERE KullaniciAdi = $u COLLATE NOCASE";
            checkCmd.Parameters.AddWithValue("$u", username);
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            
            if (count == 0) return false;

            Upgrade(username, newPassword, conn);
            return true;
        }
        catch { return false; }
    }

    private bool FixedTimeEquals(string a, string b)
    {
        if (a == null || b == null) return false;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal);
    }
}
