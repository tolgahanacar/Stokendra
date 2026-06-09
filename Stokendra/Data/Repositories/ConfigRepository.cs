using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System.Globalization;
using System.Collections.Generic;
using System;

namespace Stokendra.Data.Repositories;

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

    public List<Unit> GetUnits()
    {
        var list = new List<Unit>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Units ORDER BY Name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Unit { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        }
        return list;
    }

    public void CreateBackup(string destinationPath)
    {
        using var source = ConnectionFactory.CreateConnection();
        using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
        destination.Open();
        source.BackupDatabase(destination);
    }

    public void WriteAuditLog(string action, string tableName, int recordId, string details)
    {
        LogAudit(action, tableName, recordId, details);
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
