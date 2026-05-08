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
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Departmanlar SET Ad = $new WHERE Ad = $old";
        cmd.Parameters.AddWithValue("$new", newName.Trim());
        cmd.Parameters.AddWithValue("$old", oldName.Trim());
        await cmd.ExecuteNonQueryAsync();
    }
}
