using Dapper;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Repositories;

public sealed class ServiceRecordRepository(IDbConnectionFactory connectionFactory) 
    : RepositoryBase(connectionFactory), IServiceRecordRepository
{
    public async Task<List<ServiceRecord>> GetAllAsync(DateTime? start, DateTime? end, string? search, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        
        var sql = "SELECT Id, DeviceName, SerialNumber, Company, ServiceDate, Issue, Result FROM ServiceRecords WHERE 1=1";
        var @params = new DynamicParameters();

        if (start.HasValue)
        {
            sql += " AND ServiceDate >= @Start";
            @params.Add("Start", start.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (end.HasValue)
        {
            sql += " AND ServiceDate <= @End";
            @params.Add("End", end.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (DeviceName LIKE @Search OR SerialNumber LIKE @Search OR Company LIKE @Search)";
            @params.Add("Search", $"%{search}%");
        }
        
        sql += " ORDER BY ServiceDate DESC, Id DESC";

        var result = await conn.QueryAsync<ServiceRecord>(new CommandDefinition(
            sql, @params, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task AddAsync(ServiceRecord record, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(record.DeviceName)) throw new ArgumentException("Device name cannot be empty.");
        
        using var conn = ConnectionFactory.CreateConnection();
        var sql = """
            INSERT INTO ServiceRecords (DeviceName, SerialNumber, Company, ServiceDate, Issue, Result)
            VALUES (@DeviceName, @SerialNumber, @Company, @ServiceDateString, @Issue, @Result);
            SELECT last_insert_rowid();
            """;
        
        record.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, 
            new {
                record.DeviceName,
                SerialNumber = record.SerialNumber ?? "",
                Company = record.Company ?? "",
                ServiceDateString = record.ServiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Issue = record.Issue ?? "",
                Result = record.Result ?? ""
            }, 
            cancellationToken: cancellationToken));
            
        LogAudit("Insert", "ServiceRecords", record.Id, $"Device: {record.DeviceName}");
    }

    public async Task UpdateAsync(ServiceRecord record, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var sql = """
            UPDATE ServiceRecords SET 
                DeviceName=@DeviceName, SerialNumber=@SerialNumber, Company=@Company, 
                ServiceDate=@ServiceDateString, Issue=@Issue, Result=@Result 
            WHERE Id=@Id
            """;
            
        await conn.ExecuteAsync(new CommandDefinition(
            sql, 
            new {
                record.DeviceName,
                SerialNumber = record.SerialNumber ?? "",
                Company = record.Company ?? "",
                ServiceDateString = record.ServiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                record.Issue,
                record.Result,
                record.Id
            }, 
            cancellationToken: cancellationToken));
            
        LogAudit("Update", "ServiceRecords", record.Id, $"Device: {record.DeviceName}");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ServiceRecords WHERE Id=@Id", 
            new { Id = id }, 
            cancellationToken: cancellationToken));
        LogAudit("Delete", "ServiceRecords", id, "");
    }

    public async Task DeleteBulkAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try 
        {
            await conn.ExecuteAsync(new CommandDefinition("DELETE FROM ServiceRecords WHERE Id IN @Ids", new { Ids = idList }, transaction: trans, cancellationToken: cancellationToken));
            await trans.CommitAsync(cancellationToken);
            LogAudit("Delete", "ServiceRecords", 0, "Bulk ServiceRecord Delete");
        } 
        catch 
        { 
            await trans.RollbackAsync(cancellationToken); 
            throw; 
        }
    }

    public async Task AddBulkAsync(IEnumerable<ServiceRecord> records, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            var sql = """
                INSERT INTO ServiceRecords (DeviceName, SerialNumber, Company, ServiceDate, Issue, Result) 
                VALUES (@DeviceName, @SerialNumber, @Company, @ServiceDateString, @Issue, @Result)
                """;
            var data = records.Select(r => new {
                r.DeviceName,
                SerialNumber = r.SerialNumber ?? "",
                Company = r.Company ?? "",
                ServiceDateString = r.ServiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Issue = r.Issue ?? "",
                Result = r.Result ?? ""
            });
            await conn.ExecuteAsync(new CommandDefinition(sql, data, transaction: trans, cancellationToken: cancellationToken));
            await trans.CommitAsync(cancellationToken);
            LogAudit("Insert", "ServiceRecords", 0, "Bulk Insert");
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<ServiceRecord>> GetPagedAsync(int page, int pageSize, DateTime? start, DateTime? end, string? search, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        
        var @params = new DynamicParameters();
        string where = BuildWhereClause(start, end, search, @params);
        
        var sql = $"""
            SELECT Id, DeviceName, SerialNumber, Company, ServiceDate, Issue, Result 
            FROM ServiceRecords 
            {where} 
            ORDER BY ServiceDate DESC, Id DESC 
            LIMIT @Limit OFFSET @Offset
            """;
            
        @params.Add("Limit", pageSize);
        @params.Add("Offset", (page - 1) * pageSize);

        var result = await conn.QueryAsync<ServiceRecord>(new CommandDefinition(
            sql, @params, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<int> GetCountAsync(DateTime? start, DateTime? end, string? search, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        string where = BuildWhereClause(start, end, search, @params);
        
        var sql = $"SELECT COUNT(*) FROM ServiceRecords {where}";
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, @params, cancellationToken: cancellationToken));
    }

    private string BuildWhereClause(DateTime? start, DateTime? end, string? search, DynamicParameters @params)
    {
        var conditions = new List<string> { "1=1" };
        if (start.HasValue)
        {
            conditions.Add("ServiceDate >= @Start");
            @params.Add("Start", start.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (end.HasValue)
        {
            conditions.Add("ServiceDate <= @End");
            @params.Add("End", end.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(DeviceName LIKE @Search OR SerialNumber LIKE @Search OR Company LIKE @Search OR Issue LIKE @Search OR Result LIKE @Search)");
            @params.Add("Search", $"%{search}%");
        }
        return "WHERE " + string.Join(" AND ", conditions);
    }
}
