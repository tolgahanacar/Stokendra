using Dapper;
using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using System;
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
            LogAudit(conn, null, type, table, recordId, detail);
        }
        catch (Exception ex)
        {
            // Audit log hatası ana işlemi bozmamalı
            System.Diagnostics.Debug.WriteLine($"AuditLog error: {ex.Message}");
        }
    }

    /// <summary>
    /// Var olan bağlantı ve transaction üzerinde denetim kaydı oluşturur (SQLite yazma kilitlenmesini önler).
    /// </summary>
    protected static void LogAudit(SqliteConnection conn, SqliteTransaction? trans, string type, string table, int recordId, string detail)
    {
        try
        {
            const string sql = """
                INSERT INTO AuditLog (Date, Action, TableName, RecordId, Details)
                VALUES (@Date, @Action, @TableName, @RecordId, @Details)
                """;
            conn.Execute(sql, new
            {
                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Action = type,
                TableName = table,
                RecordId = recordId,
                Details = detail ?? ""
            }, transaction: trans);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AuditLog error: {ex.Message}");
        }
    }
}
