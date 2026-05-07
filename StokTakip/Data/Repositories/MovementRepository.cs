using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;

namespace StokTakip.Data.Repositories;

public sealed class MovementRepository : IMovementRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MovementRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<StokHareketi> GetAll(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType, string? category)
    {
        var list = new List<StokHareketi>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(stockCardId, startDate, endDate, department, movementType, category);
        BindQueryParams(cmd, stockCardId, startDate, endDate, department, movementType, category);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(Read(reader));
        return list;
    }

    public async Task<List<StokHareketi>> GetAllAsync(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType, string? category)
    {
        var list = new List<StokHareketi>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(stockCardId, startDate, endDate, department, movementType, category);
        BindQueryParams(cmd, stockCardId, startDate, endDate, department, movementType, category);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(Read(reader));
        return list;
    }

    public void Add(StokHareketi movement)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO StokHareketleri (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama)
            VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
        BindMovementParams(cmd, movement);
        cmd.ExecuteNonQuery();
        movement.Id = GetLastId(conn);
    }

    public async Task AddAsync(StokHareketi movement)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO StokHareketleri (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama)
            VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
        BindMovementParams(cmd, movement);
        await cmd.ExecuteNonQueryAsync();
        movement.Id = GetLastId(conn);
    }

    public void AddBulk(IEnumerable<StokHareketi> movements)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            foreach(var m in movements) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "INSERT INTO StokHareketleri (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama) VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
                BindMovementParams(cmd, m);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

    public async Task AddBulkAsync(IEnumerable<StokHareketi> movements)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            foreach(var m in movements) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "INSERT INTO StokHareketleri (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama) VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
                BindMovementParams(cmd, m);
                await cmd.ExecuteNonQueryAsync();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

    public void Update(StokHareketi movement)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE StokHareketleri SET 
                StokKartId=$sk, Tur=$tr, Miktar=$mk, KimeVerildi=$kv, 
                Departman=$dp, Tarih=$th, Aciklama=$ac 
            WHERE Id=$id";
        BindMovementParams(cmd, movement);
        cmd.Parameters.AddWithValue("$id", movement.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM StokHareketleri WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteBulk(IEnumerable<int> ids)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            foreach(var id in ids) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "DELETE FROM StokHareketleri WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

    public void UpdateBulk(IEnumerable<StokHareketi> movements)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            foreach(var m in movements) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "UPDATE StokHareketleri SET StokKartId=$sk, Tur=$tr, Miktar=$mk, KimeVerildi=$kv, Departman=$dp, Tarih=$th, Aciklama=$ac WHERE Id=$id";
                BindMovementParams(cmd, m);
                cmd.Parameters.AddWithValue("$id", m.Id);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

    public async Task<List<(DateTime Date, double Entry, double Exit)>> GetLast7DaysSummaryAsync()
    {
        var list = new List<(DateTime, double, double)>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT DATE(Tarih) as Day,
            SUM(CASE WHEN Tur='Giris' THEN Miktar ELSE 0 END) as Entry,
            SUM(CASE WHEN Tur='Cikis' THEN Miktar ELSE 0 END) as Exit
            FROM StokHareketleri
            WHERE Tarih >= $bas
            GROUP BY DATE(Tarih)
            ORDER BY Day";
        cmd.Parameters.AddWithValue("$bas", DateTime.Today.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        
        using var reader = await cmd.ExecuteReaderAsync();
        var data = new Dictionary<DateTime, (double e, double x)>();
        while(await reader.ReadAsync()) {
            data[DateTime.Parse(reader.GetString(0))] = (reader.GetDouble(1), reader.GetDouble(2));
        }
        
        for(int i=0; i<7; i++) {
            var day = DateTime.Today.AddDays(-6+i);
            if(data.TryGetValue(day, out var vals)) list.Add((day, vals.e, vals.x));
            else list.Add((day, 0, 0));
        }
        return list;
    }

    public List<string> GetDeliveredPersons()
    {
        var list = new List<string>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT KimeVerildi FROM StokHareketleri WHERE KimeVerildi IS NOT NULL AND KimeVerildi <> '' ORDER BY KimeVerildi";
        using var reader = cmd.ExecuteReader();
        while(reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    private string BuildQuery(int? cardId, DateTime? start, DateTime? end, string? dept, string? type, string? cat)
    {
        var sql = "SELECT h.*, s.Ad as StokKartAd, s.KodNo as StokKartKodNo FROM StokHareketleri h JOIN StokKartlari s ON h.StokKartId = s.Id WHERE 1=1";
        if(cardId.HasValue) sql += " AND h.StokKartId=$sk";
        if(start.HasValue) sql += " AND h.Tarih >= $ts";
        if(end.HasValue) sql += " AND h.Tarih < $te";
        if(!string.IsNullOrEmpty(dept)) sql += " AND h.Departman=$dp";
        if(!string.IsNullOrEmpty(type)) sql += " AND h.Tur=$tr";
        if(!string.IsNullOrEmpty(cat)) sql += " AND s.Kategori=$ct";
        sql += " ORDER BY h.Tarih DESC, h.Id DESC";
        return sql;
    }

    private void BindQueryParams(SqliteCommand cmd, int? cardId, DateTime? start, DateTime? end, string? dept, string? type, string? cat)
    {
        if(cardId.HasValue) cmd.Parameters.AddWithValue("$sk", cardId.Value);
        if(start.HasValue) cmd.Parameters.AddWithValue("$ts", start.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if(end.HasValue) cmd.Parameters.AddWithValue("$te", end.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if(!string.IsNullOrEmpty(dept)) cmd.Parameters.AddWithValue("$dp", dept);
        if(!string.IsNullOrEmpty(type)) cmd.Parameters.AddWithValue("$tr", type);
        if(!string.IsNullOrEmpty(cat)) cmd.Parameters.AddWithValue("$ct", cat);
    }

    private void BindMovementParams(SqliteCommand cmd, StokHareketi m)
    {
        cmd.Parameters.AddWithValue("$sk", m.StokKartId);
        cmd.Parameters.AddWithValue("$tr", m.Tur);
        cmd.Parameters.AddWithValue("$mk", m.Miktar);
        cmd.Parameters.AddWithValue("$kv", m.TeslimEdilen ?? "");
        cmd.Parameters.AddWithValue("$dp", m.Departman ?? "");
        cmd.Parameters.AddWithValue("$th", m.Tarih.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$ac", m.Aciklama ?? "");
    }

    private StokHareketi Read(SqliteDataReader reader)
    {
        return new StokHareketi {
            Id = reader.GetInt32(0),
            StokKartId = reader.GetInt32(1),
            Tur = reader.GetString(2),
            Miktar = reader.GetDouble(3),
            TeslimEdilen = reader.IsDBNull(4) ? "" : reader.GetString(4),
            Departman = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Tarih = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
            Aciklama = reader.IsDBNull(7) ? "" : reader.GetString(7),
            StokKartAd = reader.GetString(8),
            StokKartKodNo = reader.GetString(9)
        };
    }

    private int GetLastId(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
