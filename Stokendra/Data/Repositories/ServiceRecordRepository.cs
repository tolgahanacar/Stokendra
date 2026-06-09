using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Repositories;

public sealed class ServiceRecordRepository : RepositoryBase, IServiceRecordRepository
{
    public ServiceRecordRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<List<ServiceRecord>> GetAllAsync(DateTime? start, DateTime? end, string? search, CancellationToken cancellationToken = default)
    {
        var list = new List<ServiceRecord>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        
        string sql = "SELECT Id, DeviceName, SerialNumber, Company, ServiceDate, Issue, Result FROM ServiceRecords WHERE 1=1";
        if (start.HasValue) sql += " AND ServiceDate >= $s";
        if (end.HasValue) sql += " AND ServiceDate <= $e";
        if (!string.IsNullOrWhiteSpace(search)) sql += " AND (DeviceName LIKE $q OR SerialNumber LIKE $q OR Company LIKE $q)";
        
        sql += " ORDER BY ServiceDate DESC, Id DESC";
        cmd.CommandText = sql;

        if (start.HasValue) cmd.Parameters.AddWithValue("$s", start.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (end.HasValue) cmd.Parameters.AddWithValue("$e", end.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("$q", $"%{search}%");

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            DateTime serviceDate = DateTime.Now;
            if (!reader.IsDBNull(4))
            {
                string dateStr = reader.GetString(4);
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out serviceDate))
                {
                    DateTime.TryParse(dateStr, out serviceDate);
                }
            }

            list.Add(new ServiceRecord {
                Id = reader.GetInt32(0),
                DeviceName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                SerialNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Company = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ServiceDate = serviceDate,
                Issue = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Result = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }
        return list;
    }

    public async Task AddAsync(ServiceRecord record, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(record.DeviceName)) throw new ArgumentException("Device name cannot be empty.");
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ServiceRecords (DeviceName, SerialNumber, Company, ServiceDate, Issue, Result)
            VALUES ($ca, $sn, $f, $bt, $sr, $sc)";
        BindParams(cmd, record);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        record.Id = GetLastId(conn);
        LogAudit("Insert", "ServiceRecords", record.Id, $"Device: {record.DeviceName}");
    }

    private int GetLastId(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public async Task UpdateAsync(ServiceRecord record, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE ServiceRecords SET 
                DeviceName=$ca, SerialNumber=$sn, Company=$f, 
                ServiceDate=$bt, Issue=$sr, Result=$sc 
            WHERE Id=$id";
        BindParams(cmd, record);
        cmd.Parameters.AddWithValue("$id", record.Id);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        LogAudit("Update", "ServiceRecords", record.Id, $"Device: {record.DeviceName}");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ServiceRecords WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        LogAudit("Delete", "ServiceRecords", id, "");
    }

    public async Task AddBulkAsync(IEnumerable<ServiceRecord> records, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try {
            foreach(var r in records) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = (SqliteTransaction)trans;
                cmd.CommandText = "INSERT INTO ServiceRecords (DeviceName, SerialNumber, Company, ServiceDate, Issue, Result) VALUES ($ca, $sn, $f, $bt, $sr, $sc)";
                BindParams(cmd, r);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            await trans.CommitAsync(cancellationToken);
            LogAudit("Insert", "ServiceRecords", 0, "Bulk Insert");
        } catch { await trans.RollbackAsync(cancellationToken); throw; }
    }

    public async Task<List<ServiceRecord>> GetPagedAsync(int page, int pageSize, DateTime? start, DateTime? end, string? search, CancellationToken cancellationToken = default)
    {
        var list = new List<ServiceRecord>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        
        string where = BuildWhereClause(start, end, search);
        cmd.CommandText = $"SELECT Id, DeviceName, SerialNumber, Company, ServiceDate, Issue, Result FROM ServiceRecords {where} ORDER BY ServiceDate DESC, Id DESC LIMIT $limit OFFSET $offset";
        
        BindFilterParams(cmd, start, end, search);
        cmd.Parameters.AddWithValue("$limit", pageSize);
        cmd.Parameters.AddWithValue("$offset", (page - 1) * pageSize);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            DateTime serviceDate = DateTime.Now;
            if (!reader.IsDBNull(4)) {
                string dateStr = reader.GetString(4);
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out serviceDate))
                    DateTime.TryParse(dateStr, out serviceDate);
            }

            list.Add(new ServiceRecord {
                Id = reader.GetInt32(0),
                DeviceName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                SerialNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Company = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ServiceDate = serviceDate,
                Issue = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Result = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }
        return list;
    }

    public async Task<int> GetCountAsync(DateTime? start, DateTime? end, string? search, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        string where = BuildWhereClause(start, end, search);
        cmd.CommandText = $"SELECT COUNT(*) FROM ServiceRecords {where}";
        BindFilterParams(cmd, start, end, search);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private string BuildWhereClause(DateTime? start, DateTime? end, string? search)
    {
        var conditions = new List<string> { "1=1" };
        if (start.HasValue) conditions.Add("ServiceDate >= $s");
        if (end.HasValue) conditions.Add("ServiceDate <= $e");
        if (!string.IsNullOrWhiteSpace(search)) conditions.Add("(DeviceName LIKE $q OR SerialNumber LIKE $q OR Company LIKE $q OR Issue LIKE $q OR Result LIKE $q)");
        return "WHERE " + string.Join(" AND ", conditions);
    }

    private void BindFilterParams(SqliteCommand cmd, DateTime? start, DateTime? end, string? search)
    {
        if (start.HasValue) cmd.Parameters.AddWithValue("$s", start.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (end.HasValue) cmd.Parameters.AddWithValue("$e", end.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("$q", $"%{search}%");
    }

    private void BindParams(SqliteCommand cmd, ServiceRecord s)
    {
        cmd.Parameters.AddWithValue("$ca", s.DeviceName);
        cmd.Parameters.AddWithValue("$sn", s.SerialNumber ?? "");
        cmd.Parameters.AddWithValue("$f", s.Company ?? "");
        cmd.Parameters.AddWithValue("$bt", s.ServiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$sr", s.Issue ?? "");
        cmd.Parameters.AddWithValue("$sc", s.Result ?? "");
    }
}
