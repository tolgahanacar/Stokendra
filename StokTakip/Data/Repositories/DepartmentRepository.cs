using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using System.Globalization;

namespace StokTakip.Data.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DepartmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<string> GetAll()
    {
        var list = new List<string>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Ad FROM Departmanlar ORDER BY Ad";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    public void Add(string name)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Departmanlar (Ad) VALUES ($n)";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        cmd.ExecuteNonQuery();
    }

    public void Delete(string name)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Departmanlar WHERE Ad = $n";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        cmd.ExecuteNonQuery();
    }
}
