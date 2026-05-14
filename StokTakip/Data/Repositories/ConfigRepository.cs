using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;
using System.Collections.Generic;
using System;

namespace StokTakip.Data.Repositories;

public sealed class ConfigRepository : RepositoryBase, IConfigRepository
{
    public ConfigRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public string GetConfig(string key, string defaultValue = "")
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppConfig WHERE Key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        var res = cmd.ExecuteScalar();
        return res != null && res != DBNull.Value ? res.ToString()! : defaultValue;
    }

    public void SetConfig(string key, string value)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO AppConfig (Key, Value) VALUES ($k, $v) ON CONFLICT(Key) DO UPDATE SET Value=$v";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value ?? "");
        cmd.ExecuteNonQuery();
    }

    public List<Birim> GetUnits()
    {
        var list = new List<Birim>();
        using var conn = ConnectionFactory.CreateConnection();
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
        using var source = ConnectionFactory.CreateConnection();
        using var destination = new SqliteConnection($"Data Source={destinationPath}");
        destination.Open();
        source.BackupDatabase(destination);
    }

    public void WriteAuditLog(string type, string table, int recordId, string detail)
    {
        LogAudit(type, table, recordId, detail);
    }

    public void TruncateAuditLog()
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM AuditLog";
        cmd.ExecuteNonQuery();
        
        using var cmdReset = conn.CreateCommand();
        cmdReset.CommandText = "DELETE FROM sqlite_sequence WHERE name='AuditLog'";
        cmdReset.ExecuteNonQuery();
    }
}
