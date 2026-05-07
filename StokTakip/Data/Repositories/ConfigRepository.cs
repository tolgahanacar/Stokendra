using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;

namespace StokTakip.Data.Repositories;

public sealed class ConfigRepository : IConfigRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConfigRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public string GetConfig(string key, string defaultValue = "")
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Değer FROM AppConfig WHERE Anahtar = $k";
        cmd.Parameters.AddWithValue("$k", key);
        var res = cmd.ExecuteScalar();
        return res != null && res != DBNull.Value ? res.ToString()! : defaultValue;
    }

    public void SetConfig(string key, string value)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO AppConfig (Anahtar, Değer) VALUES ($k, $v) ON CONFLICT(Anahtar) DO UPDATE SET Değer=$v";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value ?? "");
        cmd.ExecuteNonQuery();
    }

    public List<Birim> GetUnits()
    {
        var list = new List<Birim>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Ad FROM Birimler ORDER BY Ad";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Birim { Id = reader.GetInt32(0), Ad = reader.GetString(1) });
        }
        return list;
    }

    public void CreateBackup(string destinationPath)
    {
        using var source = _connectionFactory.CreateConnection();
        using var destination = new SqliteConnection($"Data Source={destinationPath}");
        destination.Open();
        source.BackupDatabase(destination);
    }

    public void WriteAuditLog(string type, string table, int recordId, string detail)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO AuditLog (Tarih, IslemTipi, TabloAdi, KayitId, Detay)
            VALUES ($t, $it, $ta, $id, $d)";
        cmd.Parameters.AddWithValue("$t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$it", type);
        cmd.Parameters.AddWithValue("$ta", table);
        cmd.Parameters.AddWithValue("$id", recordId);
        cmd.Parameters.AddWithValue("$d", detail ?? "");
        cmd.ExecuteNonQuery();
    }
}
