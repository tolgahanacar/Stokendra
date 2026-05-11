using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StokTakip.Data.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DepartmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<string>> GetAllAsync()
    {
        var list = new List<string>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Ad FROM Departmanlar ORDER BY Ad";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(reader.GetString(0));
        return list;
    }

    public async Task AddAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Departmanlar (Ad) VALUES ($n)";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Departmanlar WHERE Ad = $n";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;

        using var conn = _connectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync();
        try {
            // 1. Departmanlar tablosunu güncelle
            using var cmd1 = conn.CreateCommand();
            cmd1.Transaction = (SqliteTransaction)trans;
            cmd1.CommandText = "UPDATE Departmanlar SET Ad = $new WHERE Ad = $old";
            cmd1.Parameters.AddWithValue("$new", newName.Trim());
            cmd1.Parameters.AddWithValue("$old", oldName.Trim());
            await cmd1.ExecuteNonQueryAsync();

            // 2. Stok Hareketleri tablosunu güncelle (Cascade)
            using var cmd2 = conn.CreateCommand();
            cmd2.Transaction = (SqliteTransaction)trans;
            cmd2.CommandText = "UPDATE StokHareketleri SET Departman = $new WHERE Departman = $old";
            cmd2.Parameters.AddWithValue("$new", newName.Trim());
            cmd2.Parameters.AddWithValue("$old", oldName.Trim());
            await cmd2.ExecuteNonQueryAsync();

            await trans.CommitAsync();
        } catch { await trans.RollbackAsync(); throw; }
    }
}
