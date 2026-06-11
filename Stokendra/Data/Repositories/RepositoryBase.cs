using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using System.Globalization;

namespace Stokendra.Data.Repositories;

public abstract class RepositoryBase(IDbConnectionFactory connectionFactory)
{
    protected readonly IDbConnectionFactory ConnectionFactory = connectionFactory;

    protected void LogAudit(string type, string table, int recordId, string detail)
    {
        try
        {
            using var conn = ConnectionFactory.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AuditLog (Date, Action, TableName, RecordId, Details)
                VALUES ($t, $it, $ta, $id, $d)";
            cmd.Parameters.AddWithValue("$t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$it", type);
            cmd.Parameters.AddWithValue("$ta", table);
            cmd.Parameters.AddWithValue("$id", recordId);
            cmd.Parameters.AddWithValue("$d", detail ?? "");
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Audit log hatası ana işlemi bozmamalı
            System.Diagnostics.Debug.WriteLine($"AuditLog error: {ex.Message}");
        }
    }
}
