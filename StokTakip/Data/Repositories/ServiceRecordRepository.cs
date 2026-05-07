using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;

namespace StokTakip.Data.Repositories;

public sealed class ServiceRecordRepository : IServiceRecordRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ServiceRecordRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<ServisKaydi> GetAll(DateTime? start, DateTime? end, string? search)
    {
        var list = new List<ServisKaydi>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        
        string sql = "SELECT Id, CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc FROM ServisKayitlari WHERE 1=1";
        if (start.HasValue) sql += " AND BakimTarihi >= $s";
        if (end.HasValue) sql += " AND BakimTarihi <= $e";
        if (!string.IsNullOrWhiteSpace(search)) sql += " AND (CihazAdi LIKE $q OR SeriNumarasi LIKE $q OR Firma LIKE $q)";
        
        sql += " ORDER BY BakimTarihi DESC, Id DESC";
        cmd.CommandText = sql;

        if (start.HasValue) cmd.Parameters.AddWithValue("$s", start.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (end.HasValue) cmd.Parameters.AddWithValue("$e", end.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("$q", $"%{search}%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            DateTime bakimTarihi = DateTime.Now;
            if (!reader.IsDBNull(4))
            {
                string dateStr = reader.GetString(4);
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out bakimTarihi))
                {
                    // Fallback to legacy formats if needed
                    DateTime.TryParse(dateStr, out bakimTarihi);
                }
            }

            list.Add(new ServisKaydi {
                Id = reader.GetInt32(0),
                CihazAdi = reader.IsDBNull(1) ? "" : reader.GetString(1),
                SeriNumarasi = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Firma = reader.IsDBNull(3) ? "" : reader.GetString(3),
                BakimTarihi = bakimTarihi,
                Sorun = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Sonuc = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }
        return list;
    }

    public void Add(ServisKaydi record)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ServisKayitlari (CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc)
            VALUES ($ca, $sn, $f, $bt, $sr, $sc)";
        BindParams(cmd, record);
        cmd.ExecuteNonQuery();
    }

    public void Update(ServisKaydi record)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE ServisKayitlari SET 
                CihazAdi=$ca, SeriNumarasi=$sn, Firma=$f, 
                BakimTarihi=$bt, Sorun=$sr, Sonuc=$sc 
            WHERE Id=$id";
        BindParams(cmd, record);
        cmd.Parameters.AddWithValue("$id", record.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ServisKayitlari WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void AddBulk(IEnumerable<ServisKaydi> records)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            foreach(var r in records) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "INSERT INTO ServisKayitlari (CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc) VALUES ($ca, $sn, $f, $bt, $sr, $sc)";
                BindParams(cmd, r);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

    private void BindParams(SqliteCommand cmd, ServisKaydi s)
    {
        cmd.Parameters.AddWithValue("$ca", s.CihazAdi);
        cmd.Parameters.AddWithValue("$sn", s.SeriNumarasi ?? "");
        cmd.Parameters.AddWithValue("$f", s.Firma ?? "");
        cmd.Parameters.AddWithValue("$bt", s.BakimTarihi.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$sr", s.Sorun ?? "");
        cmd.Parameters.AddWithValue("$sc", s.Sonuc ?? "");
    }
}
