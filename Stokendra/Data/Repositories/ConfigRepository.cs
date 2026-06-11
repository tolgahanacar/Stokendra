using Dapper;
using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System.Collections.Generic;
using System.Linq;

namespace Stokendra.Data.Repositories;

public sealed class ConfigRepository(IDbConnectionFactory connectionFactory) 
    : RepositoryBase(connectionFactory), IConfigRepository
{
    public string GetConfig(string key, string defaultValue = "")
    {
        using var conn = ConnectionFactory.CreateConnection();
        var res = conn.ExecuteScalar<string>(
            "SELECT Value FROM AppConfig WHERE Key = @Key", 
            new { Key = key });
        return res ?? defaultValue;
    }

    public void SetConfig(string key, string value)
    {
        using var conn = ConnectionFactory.CreateConnection();
        conn.Execute(
            "INSERT INTO AppConfig (Key, Value) VALUES (@Key, @Value) ON CONFLICT(Key) DO UPDATE SET Value=@Value",
            new { Key = key, Value = value ?? "" });
    }

    public List<Unit> GetUnits()
    {
        using var conn = ConnectionFactory.CreateConnection();
        return conn.Query<Unit>("SELECT Id, Name FROM Units ORDER BY Name").ToList();
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
        conn.Execute("DELETE FROM AuditLog");
        conn.Execute("DELETE FROM sqlite_sequence WHERE name='AuditLog'");
    }
}
