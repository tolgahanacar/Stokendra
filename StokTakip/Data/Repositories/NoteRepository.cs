using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;

namespace StokTakip.Data.Repositories;

public sealed class NoteRepository : INoteRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public NoteRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<Not> GetAll()
    {
        var list = new List<Not>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Tarih, Baslik, Icerik FROM Notlar ORDER BY Tarih DESC, Id DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Not
            {
                Id = reader.GetInt32(0),
                Tarih = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                Baslik = reader.GetString(2),
                Icerik = reader.IsDBNull(3) ? "" : reader.GetString(3)
            });
        }
        return list;
    }

    public void Add(Not note)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Notlar (Tarih, Baslik, Icerik) VALUES ($t, $b, $i)";
        cmd.Parameters.AddWithValue("$t", note.Tarih.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$b", note.Baslik);
        cmd.Parameters.AddWithValue("$i", note.Icerik ?? "");
        cmd.ExecuteNonQuery();
    }

    public void Update(Not note)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Notlar SET Baslik=$b, Icerik=$i, Tarih=$t WHERE Id=$id";
        cmd.Parameters.AddWithValue("$b", note.Baslik);
        cmd.Parameters.AddWithValue("$i", note.Icerik ?? "");
        cmd.Parameters.AddWithValue("$t", note.Tarih.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$id", note.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Notlar WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
