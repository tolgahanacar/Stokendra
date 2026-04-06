using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Data.Sqlite;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Data;

public sealed partial class Database
{

    public bool VarsayilanAdminSifresiKullanimda()
    {
        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT SifreHash, Tuz FROM Kullanicilar WHERE KullaniciAdi='admin'");
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return false;

            string storedHash = reader.GetString(0);
            string storedSalt = reader.GetString(1);
            return VerifyPassword("admin", storedSalt, storedHash, out _);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("VarsayilanAdminSifresiKullanimda error: " + ex);
            return false;
        }
    }


    public string? SifrePolitikasiHatasi(string sifre, string? kullaniciAdi = null)
    {
        if (string.IsNullOrWhiteSpace(sifre))
            return L("password_empty");
        if (sifre.Length < 10)
            return L("password_policy_length");
        if (sifre.Any(char.IsWhiteSpace))
            return L("password_policy_whitespace");

        int score = 0;
        if (sifre.Any(char.IsLower)) score++;
        if (sifre.Any(char.IsUpper)) score++;
        if (sifre.Any(char.IsDigit)) score++;
        if (sifre.Any(ch => !char.IsLetterOrDigit(ch))) score++;

        if (score < 3)
            return L("password_policy_complexity");

        if (!string.IsNullOrWhiteSpace(kullaniciAdi) &&
            sifre.Contains(kullaniciAdi, StringComparison.OrdinalIgnoreCase))
            return L("password_policy_username");

        return null;
    }


    public bool KullaniciDogrula(string kullaniciAdi, string sifre)
    {
        string user = kullaniciAdi?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(sifre))
            return false;

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null,
                "SELECT SifreHash, Tuz FROM Kullanicilar WHERE KullaniciAdi=$u");
            command.Parameters.AddWithValue("$u", user);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return false;

            string storedHash = reader.GetString(0);
            string storedSalt = reader.GetString(1);
            bool verified = VerifyPassword(sifre, storedSalt, storedHash, out bool needsUpgrade);
            reader.Close();

            if (verified && needsUpgrade)
                UpgradePasswordHash(user, sifre, connection);

            return verified;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("KullaniciDogrula error: " + ex);
            return false;
        }
    }


    public bool SifreDegistir(string kullaniciAdi, string eskiSifre, string yeniSifre)
    {
        string user = kullaniciAdi?.Trim() ?? "";
        string? policyError = SifrePolitikasiHatasi(yeniSifre, user);
        if (policyError != null || string.Equals(eskiSifre, yeniSifre, StringComparison.Ordinal))
            return false;
        if (!KullaniciDogrula(user, eskiSifre))
            return false;

        try
        {
            string newSalt = GenerateSalt();
            string newHash = HashPasswordV3(yeniSifre, newSalt);

            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();
            using var command = CreateCommand(connection, transaction,
                "UPDATE Kullanicilar SET SifreHash=$h, Tuz=$s WHERE KullaniciAdi=$u");
            command.Parameters.AddWithValue("$h", newHash);
            command.Parameters.AddWithValue("$s", newSalt);
            command.Parameters.AddWithValue("$u", user);

            if (command.ExecuteNonQuery() == 0)
                return false;

            InsertAuditLog(connection, transaction, "SIFRE_DEGISTIR", "Kullanicilar", 0, user);
            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SifreDegistir error: " + ex);
            return false;
        }
    }


    private static string GenerateSalt()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(salt);
    }


    private static string HashPasswordV2Legacy(string password, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(salt), PasswordIterationsV2, HashAlgorithmName.SHA512);
        byte[] hash = pbkdf2.GetBytes(PasswordHashSize);
        return "v2:" + Convert.ToBase64String(hash);
    }


    private static string HashPasswordV3(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, PasswordIterationsV3, HashAlgorithmName.SHA512);
        byte[] hash = pbkdf2.GetBytes(PasswordHashSize);
        return "v3:" + Convert.ToBase64String(hash);
    }


    private static bool VerifyPassword(string password, string salt, string storedHash, out bool needsUpgrade)
    {
        needsUpgrade = false;

        if (storedHash.StartsWith("v3:", StringComparison.Ordinal))
            return FixedTimeEquals(storedHash, HashPasswordV3(password, salt));

        if (storedHash.StartsWith("v2:", StringComparison.Ordinal))
        {
            bool match = FixedTimeEquals(storedHash, HashPasswordV2Legacy(password, salt));
            needsUpgrade = match;
            return match;
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        string legacyHash = Convert.ToBase64String(bytes);
        bool legacyMatch = FixedTimeEquals(legacyHash, storedHash);
        needsUpgrade = legacyMatch;
        return legacyMatch;
    }


    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }


    private static void UpgradePasswordHash(string kullaniciAdi, string sifre, SqliteConnection connection)
    {
        string newSalt = GenerateSalt();
        string newHash = HashPasswordV3(sifre, newSalt);

        using var transaction = connection.BeginTransaction();
        using var command = CreateCommand(connection, transaction,
            "UPDATE Kullanicilar SET SifreHash=$h, Tuz=$s WHERE KullaniciAdi=$u");
        command.Parameters.AddWithValue("$h", newHash);
        command.Parameters.AddWithValue("$s", newSalt);
        command.Parameters.AddWithValue("$u", kullaniciAdi);
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
