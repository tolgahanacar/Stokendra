using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Data;

public sealed partial class Database
{

    public string GetConfig(string key, string def = "")
    {
        string normalizedKey = key?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return def;

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT Value FROM AppConfig WHERE Key=$k");
            command.Parameters.AddWithValue("$k", normalizedKey);
            return command.ExecuteScalar()?.ToString() ?? def;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("GetConfig error: " + ex);
            return def;
        }
    }


    public void SetConfig(string key, string val)
    {
        string normalizedKey = key?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new InvalidOperationException(L("config_key_required"));

        try
        {
            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();
            using var command = CreateCommand(connection, transaction,
                "INSERT INTO AppConfig (Key,Value) VALUES ($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value");
            command.Parameters.AddWithValue("$k", normalizedKey);
            command.Parameters.AddWithValue("$v", val ?? "");
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("SetConfig error: " + ex);
            throw;
        }
    }


    private static void InsertAuditLog(SqliteConnection connection, SqliteTransaction? transaction, string tip, string tablo, int id, string detay)
    {
        using var command = CreateCommand(connection, transaction,
            "INSERT INTO AuditLog (Tarih,IslemTipi,TabloAdi,KayitId,Detay) VALUES ($t,$it,$ta,$ki,$d)");
        command.Parameters.AddWithValue("$t", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$it", TrimTo(tip, 80));
        command.Parameters.AddWithValue("$ta", TrimTo(tablo, 80));
        command.Parameters.AddWithValue("$ki", id);
        command.Parameters.AddWithValue("$d", TrimTo(detay, 500));
        command.ExecuteNonQuery();
        
        AppPaths.LogActivity(tip.ToLowerInvariant(), tablo.ToLowerInvariant(), detay);
    }


    public List<string> DepartmanlariGetir()
    {
        var liste = new List<string>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT Ad FROM Departmanlar ORDER BY Ad COLLATE NOCASE");
            using var reader = command.ExecuteReader();
            while (reader.Read())
                liste.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            AppLogger.LogError("DepartmanlariGetir error: " + ex);
        }

        return liste;
    }


    public void DepartmanEkle(string ad)
    {
        string normalized = NormalizeRequiredText(ad, 120, L("department_required"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction, "INSERT OR IGNORE INTO Departmanlar (Ad) VALUES ($a)");
            command.Parameters.AddWithValue("$a", normalized);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("DepartmanEkle error: " + ex);
            throw;
        }
    }


    public void DepartmanSil(string ad)
    {
        string normalized = NormalizeRequiredText(ad, 120, L("department_required"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction, "DELETE FROM Departmanlar WHERE Ad=$a");
            command.Parameters.AddWithValue("$a", normalized);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("DepartmanSil error: " + ex);
            throw;
        }
    }


    public List<Birim> BirimleriGetir()
    {
        var liste = new List<Birim>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT Id, Ad FROM Birimler ORDER BY Ad COLLATE NOCASE");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                liste.Add(new Birim
                {
                    Id = reader.GetInt32(0),
                    Ad = reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("BirimleriGetir error: " + ex);
        }

        return liste;
    }


    public List<Not> NotlariGetir()
    {
        var liste = new List<Not>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, "SELECT Id, Tarih, Baslik, Icerik FROM Notlar ORDER BY Tarih DESC, Id DESC");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                liste.Add(new Not
                {
                    Id = reader.GetInt32(0),
                    Tarih = ParseDateSafe(reader.GetString(1)),
                    Baslik = reader.GetString(2),
                    Icerik = reader.IsDBNull(3) ? "" : reader.GetString(3)
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("NotlariGetir error: " + ex);
        }

        return liste;
    }


    public void NotEkle(Not not)
    {
        Not normalized = NormalizeNot(not);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction,
                "INSERT INTO Notlar (Tarih, Baslik, Icerik) VALUES ($t, $b, $i)");
            command.Parameters.AddWithValue("$t", normalized.Tarih.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$b", normalized.Baslik);
            command.Parameters.AddWithValue("$i", normalized.Icerik);
            command.ExecuteNonQuery();
            not.Id = GetLastInsertRowId(connection, transaction);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("NotEkle error: " + ex);
            throw;
        }
    }


    public void NotGuncelle(Not not)
    {
        Not normalized = NormalizeNot(not);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction,
                "UPDATE Notlar SET Baslik=$b, Icerik=$i, Tarih=$t WHERE Id=$id");
            command.Parameters.AddWithValue("$b", normalized.Baslik);
            command.Parameters.AddWithValue("$i", normalized.Icerik);
            command.Parameters.AddWithValue("$t", normalized.Tarih.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$id", normalized.Id);

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("NotGuncelle error: " + ex);
            throw;
        }
    }


    public void NotSil(int id)
    {
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = CreateCommand(connection, transaction, "DELETE FROM Notlar WHERE Id=$id");
            command.Parameters.AddWithValue("$id", id);

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("NotSil error: " + ex);
            throw;
        }
    }


    private static Not NormalizeNot(Not not)
    {
        return new Not
        {
            Id = not.Id,
            Tarih = not.Tarih == default ? DateTime.Now : not.Tarih,
            Baslik = NormalizeRequiredText(not.Baslik, 150, L("title_empty")),
            Icerik = TrimTo(not.Icerik, 4000)
        };
    }
}
