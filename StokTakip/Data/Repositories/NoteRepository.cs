using Dapper;
using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StokTakip.Data.Repositories;

public sealed class NoteRepository : RepositoryBase, INoteRepository
{
    public NoteRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public List<Not> GetAll()
    {
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "SELECT Id, Tarih, Baslik, Icerik, OlusturmaTarihi, GuncellenmeTarihi FROM Notlar ORDER BY OlusturmaTarihi DESC, Id DESC";
        // SQLite Tarih formatları Dapper tarafından otomatik maplenir (ISO 8601 uyumlu kaydedildiğini varsayıyoruz).
        return conn.Query<Not>(sql).ToList();
    }

    public async Task<List<Not>> GetAllAsync()
    {
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "SELECT Id, Tarih, Baslik, Icerik, OlusturmaTarihi, GuncellenmeTarihi FROM Notlar ORDER BY OlusturmaTarihi DESC, Id DESC";
        var result = await conn.QueryAsync<Not>(sql);
        return result.ToList();
    }

    public void Add(Not note)
    {
        if (string.IsNullOrWhiteSpace(note.Baslik)) throw new InvalidOperationException("Başlık boş olamaz.");
        note.OlusturmaTarihi = DateTime.Now;
        note.GuncellenmeTarihi = DateTime.Now;

        using var conn = ConnectionFactory.CreateConnection();
        var sql = @"INSERT INTO Notlar (Tarih, Baslik, Icerik, OlusturmaTarihi, GuncellenmeTarihi) 
                    VALUES (@Tarih, @Baslik, @Icerik, @OlusturmaTarihi, @GuncellenmeTarihi);
                    SELECT last_insert_rowid();";
        
        note.Id = conn.ExecuteScalar<int>(sql, new {
            Tarih = note.Tarih.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            note.Baslik,
            Icerik = note.Icerik ?? "",
            OlusturmaTarihi = note.OlusturmaTarihi.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            GuncellenmeTarihi = note.GuncellenmeTarihi.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
        
        LogAudit("Ekleme", "Notlar", note.Id, $"Başlık: {note.Baslik}");
    }

    public void Update(Not note)
    {
        note.GuncellenmeTarihi = DateTime.Now;
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "UPDATE Notlar SET Baslik=@Baslik, Icerik=@Icerik, GuncellenmeTarihi=@GuncellenmeTarihi WHERE Id=@Id";
        
        conn.Execute(sql, new {
            note.Baslik,
            Icerik = note.Icerik ?? "",
            GuncellenmeTarihi = note.GuncellenmeTarihi.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            note.Id
        });
        
        LogAudit("Guncelleme", "Notlar", note.Id, $"Başlık: {note.Baslik}");
    }

    public void Delete(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        conn.Execute("DELETE FROM Notlar WHERE Id=@Id", new { Id = id });
        LogAudit("Silme", "Notlar", id, "");
    }

    public void DeleteBulk(IEnumerable<int> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            conn.Execute("DELETE FROM Notlar WHERE Id IN @Ids", new { Ids = idList }, trans);
            trans.Commit();
            LogAudit("Silme", "Notlar", 0, "Toplu Not Silme");
        } catch { trans.Rollback(); throw; }
    }
}
