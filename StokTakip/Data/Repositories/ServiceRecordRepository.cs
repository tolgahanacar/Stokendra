using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StokTakip.Data.Repositories;

public sealed class ServiceRecordRepository : RepositoryBase, IServiceRecordRepository
{
    public ServiceRecordRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<List<ServisKaydi>> GetAllAsync(DateTime? start, DateTime? end, string? search)
    {
        var list = new List<ServisKaydi>();
        using var conn = ConnectionFactory.CreateConnection();
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

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            DateTime bakimTarihi = DateTime.Now;
            if (!reader.IsDBNull(4))
            {
                string dateStr = reader.GetString(4);
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out bakimTarihi))
                {
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

    public async Task AddAsync(ServisKaydi record)
    {
        if (string.IsNullOrWhiteSpace(record.CihazAdi)) throw new System.Exception("Cihaz adı boş olamaz.");
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ServisKayitlari (CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc)
            VALUES ($ca, $sn, $f, $bt, $sr, $sc)";
        BindParams(cmd, record);
        await cmd.ExecuteNonQueryAsync();
        record.Id = GetLastId(conn);
        LogAudit("Ekleme", "ServisKayitlari", record.Id, $"Cihaz: {record.CihazAdi}");
    }

    private int GetLastId(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public async Task UpdateAsync(ServisKaydi record)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE ServisKayitlari SET 
                CihazAdi=$ca, SeriNumarasi=$sn, Firma=$f, 
                BakimTarihi=$bt, Sorun=$sr, Sonuc=$sc 
            WHERE Id=$id";
        BindParams(cmd, record);
        cmd.Parameters.AddWithValue("$id", record.Id);
        await cmd.ExecuteNonQueryAsync();
        LogAudit("Guncelleme", "ServisKayitlari", record.Id, $"Cihaz: {record.CihazAdi}");
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ServisKayitlari WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
        LogAudit("Silme", "ServisKayitlari", id, "");
    }

    public async Task AddBulkAsync(IEnumerable<ServisKaydi> records)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync();
        try {
            foreach(var r in records) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = (SqliteTransaction)trans;
                cmd.CommandText = "INSERT INTO ServisKayitlari (CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc) VALUES ($ca, $sn, $f, $bt, $sr, $sc)";
                BindParams(cmd, r);
                await cmd.ExecuteNonQueryAsync();
            }
            await trans.CommitAsync();
            LogAudit("Ekleme", "ServisKayitlari", 0, "Toplu Servis Kaydı Ekleme");
        } catch { await trans.RollbackAsync(); throw; }
    }

    public async Task<List<ServisKaydi>> GetPagedAsync(int page, int pageSize, DateTime? start, DateTime? end, string? search)
    {
        var list = new List<ServisKaydi>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        
        string where = BuildWhereClause(start, end, search);
        cmd.CommandText = $"SELECT Id, CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc FROM ServisKayitlari {where} ORDER BY BakimTarihi DESC, Id DESC LIMIT $limit OFFSET $offset";
        
        BindFilterParams(cmd, start, end, search);
        cmd.Parameters.AddWithValue("$limit", pageSize);
        cmd.Parameters.AddWithValue("$offset", (page - 1) * pageSize);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            DateTime bakimTarihi = DateTime.Now;
            if (!reader.IsDBNull(4)) {
                string dateStr = reader.GetString(4);
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out bakimTarihi))
                    DateTime.TryParse(dateStr, out bakimTarihi);
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

    public async Task<int> GetCountAsync(DateTime? start, DateTime? end, string? search)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        string where = BuildWhereClause(start, end, search);
        cmd.CommandText = $"SELECT COUNT(*) FROM ServisKayitlari {where}";
        BindFilterParams(cmd, start, end, search);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private string BuildWhereClause(DateTime? start, DateTime? end, string? search)
    {
        var conditions = new List<string> { "1=1" };
        if (start.HasValue) conditions.Add("BakimTarihi >= $s");
        if (end.HasValue) conditions.Add("BakimTarihi <= $e");
        if (!string.IsNullOrWhiteSpace(search)) conditions.Add("(CihazAdi LIKE $q OR SeriNumarasi LIKE $q OR Firma LIKE $q OR Sorun LIKE $q OR Sonuc LIKE $q)");
        return "WHERE " + string.Join(" AND ", conditions);
    }

    private void BindFilterParams(SqliteCommand cmd, DateTime? start, DateTime? end, string? search)
    {
        if (start.HasValue) cmd.Parameters.AddWithValue("$s", start.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (end.HasValue) cmd.Parameters.AddWithValue("$e", end.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("$q", $"%{search}%");
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
