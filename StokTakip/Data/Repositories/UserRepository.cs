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

    public bool Authenticate(string username, string password)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SifreHash, Tuz FROM Kullanicilar WHERE KullaniciAdi = $u";
        cmd.Parameters.AddWithValue("$u", username);
        
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return false;
        
        string storedHash = reader.GetString(0);
        string salt = reader.GetString(1);
        
        bool match = Verify(password, salt, storedHash, out bool upgrade);
        if (match && upgrade) Upgrade(username, password, conn);
        return match;
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
        string hash = Hash(password, salt);
        return FixedTimeEquals(stored, hash) || FixedTimeEquals(stored.Replace("v4:", ""), hash.Replace("v4:", ""));
    }

    private string Hash(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA512);
        return "v4:" + Convert.ToBase64String(pbkdf2.GetBytes(HashSize));
    }

    private void Upgrade(string user, string pass, SqliteConnection conn)
    {
        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        string hash = Hash(pass, salt);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Kullanicilar SET SifreHash=$h, Tuz=$s WHERE KullaniciAdi=$u";
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$s", salt);
        cmd.Parameters.AddWithValue("$u", user);
        cmd.ExecuteNonQuery();
    }

    private bool FixedTimeEquals(string a, string b)
    {
        byte[] ba = Encoding.UTF8.GetBytes(a);
        byte[] bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
