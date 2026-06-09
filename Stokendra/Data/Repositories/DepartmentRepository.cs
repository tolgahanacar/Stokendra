using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DepartmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Departments ORDER BY Name";
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) list.Add(reader.GetString(0));
        return list;
    }

    public async Task AddAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Departments (Name) VALUES ($n)";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Departments WHERE Name = $n";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;

        using var conn = _connectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try {
            // 1. Update Departments table
            using var cmd1 = conn.CreateCommand();
            cmd1.Transaction = (SqliteTransaction)trans;
            cmd1.CommandText = "UPDATE Departments SET Name = $new WHERE Name = $old";
            cmd1.Parameters.AddWithValue("$new", newName.Trim());
            cmd1.Parameters.AddWithValue("$old", oldName.Trim());
            await cmd1.ExecuteNonQueryAsync(cancellationToken);

            // 2. Update StockMovements table (Cascade)
            using var cmd2 = conn.CreateCommand();
            cmd2.Transaction = (SqliteTransaction)trans;
            cmd2.CommandText = "UPDATE StockMovements SET Department = $new WHERE Department = $old";
            cmd2.Parameters.AddWithValue("$new", newName.Trim());
            cmd2.Parameters.AddWithValue("$old", oldName.Trim());
            await cmd2.ExecuteNonQueryAsync(cancellationToken);

            await trans.CommitAsync(cancellationToken);
        } catch { await trans.RollbackAsync(cancellationToken); throw; }
    }
}
