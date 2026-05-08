using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;

namespace StokTakip.Data.Repositories;

public sealed class MovementRepository : RepositoryBase, IMovementRepository
{
    public MovementRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public List<StokHareketi> GetAll(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType, string? category)
    {
        var list = new List<StokHareketi>();
        using var conn = ConnectionFactory.CreateConnection();
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
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(stockCardId, startDate, endDate, department, movementType, category);
        BindQueryParams(cmd, stockCardId, startDate, endDate, department, movementType, category);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(Read(reader));
        return list;
    }

    public void Add(StokHareketi movement)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateMovement(movement, conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO StokHareketleri (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama)
            VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
        BindMovementParams(cmd, movement);
        cmd.ExecuteNonQuery();
        movement.Id = GetLastId(conn);
        LogAudit("Ekleme", "StokHareketleri", movement.Id, $"{movement.Tur}: {movement.Miktar}");
    }

    public async Task AddAsync(StokHareketi movement)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateMovement(movement, conn);
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
        var list = movements.ToList();
        if (list.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        // IMMEDIATE transaction prevents race conditions during validation
        using var trans = conn.BeginTransaction();
        try {
            // Pre-fetch all balances for affected cards to avoid N+1
            var cardIds = list.Select(m => m.StokKartId).Distinct().ToList();
            var balances = GetBalances(conn, trans, cardIds);

            foreach(var m in list) {
                if (!balances.TryGetValue(m.StokKartId, out var info))
                    throw new InvalidOperationException($"Stok kartı bulunamadı: ID {m.StokKartId}");

                if (info.Type == "Ust")
                    throw new InvalidOperationException($"'{info.Name}' bir üst karttır, hareket eklenemez.");

                if (m.Tur == "Cikis" && info.Balance < m.Miktar)
                    throw new InvalidOperationException($"'{info.Name}' için yetersiz stok. Mevcut: {info.Balance}, İstenen: {m.Miktar}");

                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "INSERT INTO StokHareketleri (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama) VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
                BindMovementParams(cmd, m);
                cmd.ExecuteNonQuery();

                // Update local balance for subsequent items in the same bulk
                info.Balance += (m.Tur == "Giris" ? m.Miktar : -m.Miktar);
            }
            trans.Commit();
            LogAudit("Ekleme", "StokHareketleri", 0, "Toplu Ekleme");
        } catch { trans.Rollback(); throw; }
    }

    public async Task AddBulkAsync(IEnumerable<StokHareketi> movements)
    {
        var list = movements.ToList();
        if (list.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync();
        try {
            var cardIds = list.Select(m => m.StokKartId).Distinct().ToList();
            var balances = GetBalances(conn, (SqliteTransaction)trans, cardIds);

            foreach(var m in list) {
                if (!balances.TryGetValue(m.StokKartId, out var info))
                    throw new InvalidOperationException($"Stok kartı bulunamadı: ID {m.StokKartId}");

                if (info.Type == "Ust")
                    throw new InvalidOperationException($"'{info.Name}' bir üst karttır, hareket eklenemez.");

                if (m.Tur == "Cikis" && info.Balance < m.Miktar)
                    throw new InvalidOperationException($"'{info.Name}' için yetersiz stok. Mevcut: {info.Balance}, İstenen: {m.Miktar}");

                using var cmd = conn.CreateCommand();
                cmd.Transaction = (SqliteTransaction)trans;
                cmd.CommandText = "INSERT INTO StokHareketleri (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama) VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
                BindMovementParams(cmd, m);
                await cmd.ExecuteNonQueryAsync();

                info.Balance += (m.Tur == "Giris" ? m.Miktar : -m.Miktar);
            }
            await trans.CommitAsync();
            LogAudit("Ekleme", "StokHareketleri", 0, "Toplu Ekleme (Asenkron)");
        } catch { await trans.RollbackAsync(); throw; }
    }

    private class CardBalanceInfo { public string Name; public string Type; public double Balance; }

    private Dictionary<int, CardBalanceInfo> GetBalances(SqliteConnection conn, SqliteTransaction trans, List<int> ids)
    {
        var result = new Dictionary<int, CardBalanceInfo>();
        if (ids.Count == 0) return result;

        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        string idList = string.Join(",", ids);
        cmd.CommandText = $@"
            SELECT s.Id, s.Ad, s.KartTipi,
            COALESCE((SELECT SUM(CASE WHEN Tur IN ('Giris', 'Giriş') THEN Miktar ELSE -Miktar END) FROM StokHareketleri WHERE StokKartId=s.Id), 0)
            FROM StokKartlari s WHERE s.Id IN ({idList})";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetInt32(0)] = new CardBalanceInfo {
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                Balance = reader.GetDouble(3)
            };
        }
        return result;
    }

    public void Update(StokHareketi movement)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateUpdate(movement, conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE StokHareketleri SET 
                StokKartId=$sk, Tur=$tr, Miktar=$mk, KimeVerildi=$kv, 
                Departman=$dp, Tarih=$th, Aciklama=$ac 
            WHERE Id=$id";
        BindMovementParams(cmd, movement);
        cmd.Parameters.AddWithValue("$id", movement.Id);
        cmd.ExecuteNonQuery();
        LogAudit("Guncelleme", "StokHareketleri", movement.Id, $"{movement.Tur}: {movement.Miktar}");
    }

    public void Delete(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateDeletion(new[] { id }, conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM StokHareketleri WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        LogAudit("Silme", "StokHareketleri", id, "");
    }

    public void DeleteBulk(IEnumerable<int> ids)
    {
        var list = ids.ToList();
        if (list.Count == 0) throw new InvalidOperationException("Liste boş olamaz.");

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            // Var olmayan ID kontrolü
            using (var cmdExist = conn.CreateCommand()) {
                cmdExist.Transaction = trans;
                cmdExist.CommandText = $"SELECT COUNT(*) FROM StokHareketleri WHERE Id IN ({string.Join(",", list)})";
                var count = Convert.ToInt32(cmdExist.ExecuteScalar());
                if (count != list.Count) throw new InvalidOperationException("Bazı hareketler bulunamadı.");
            }

            ValidateDeletion(list, conn, trans);
            foreach(var id in list) {
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
        using var conn = ConnectionFactory.CreateConnection();
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
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT DATE(Tarih) as Day,
            SUM(CASE WHEN Tur IN ('Giris', 'Giriş') THEN Miktar ELSE 0 END) as Entry,
            SUM(CASE WHEN Tur IN ('Cikis', 'Çıkış') THEN Miktar ELSE 0 END) as Exit
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
        using var conn = ConnectionFactory.CreateConnection();
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
        DateTime tarih = DateTime.Now;
        if (!reader.IsDBNull(6))
        {
            string dateStr = reader.GetString(6);
            if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out tarih))
            {
                DateTime.TryParse(dateStr, out tarih);
            }
        }

        return new StokHareketi {
            Id = reader.GetInt32(0),
            StokKartId = reader.GetInt32(1),
            Tur = reader.GetString(2),
            Miktar = reader.GetDouble(3),
            TeslimEdilen = reader.IsDBNull(4) ? "" : reader.GetString(4),
            Departman = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Tarih = tarih,
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

    private void ValidateMovement(StokHareketi m, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        if (double.IsNaN(m.Miktar) || double.IsInfinity(m.Miktar) || m.Miktar > 1_000_000_000) 
            throw new InvalidOperationException("Geçersiz miktar değeri.");
        
        if (m.Tur != "Giris" && m.Tur != "Cikis" && m.Tur != "Bos") throw new InvalidOperationException("Geçersiz hareket tipi.");
        
        if (m.Miktar < 0) throw new InvalidOperationException("Miktar negatif olamaz.");
        if (m.Miktar == 0 && m.Tur != "Bos") throw new InvalidOperationException("Miktar sıfır olamaz.");
        
        if (string.IsNullOrWhiteSpace(m.Tur)) throw new InvalidOperationException("Tür alanı boş olamaz.");
        if (m.StokKartId <= 0) throw new InvalidOperationException("Geçersiz stok kartı ID.");

        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        cmd.CommandText = @"
            SELECT KartTipi,
            (SELECT COALESCE(SUM(CASE WHEN Tur IN ('Giris', 'Giriş') THEN Miktar ELSE -Miktar END), 0) FROM StokHareketleri WHERE StokKartId=s.Id) as MevcutStok
            FROM StokKartlari s WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", m.StokKartId);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            if (reader.GetString(0) == "Ust") throw new InvalidOperationException("Üst kartlara hareket eklenemez.");
            if (m.Tur == "Cikis")
            {
                double mevcut = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                if (mevcut < m.Miktar) throw new InvalidOperationException("Yetersiz stok.");
            }
        }
        else throw new InvalidOperationException("Stok kartı bulunamadı.");
    }

    private void ValidateDeletion(IEnumerable<int> ids, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        // Silme işlemi sonrası herhangi bir kartın stoku negatife düşüyor mu?
        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        
        var idList = string.Join(",", ids);
        cmd.CommandText = $@"
            SELECT StokKartId FROM StokHareketleri WHERE Id IN ({idList}) GROUP BY StokKartId";
        
        var cardIds = new List<int>();
        using (var reader = cmd.ExecuteReader())
            while (reader.Read()) cardIds.Add(reader.GetInt32(0));

        foreach (var cardId in cardIds)
        {
            using var cmdCheck = conn.CreateCommand();
            if (trans != null) cmdCheck.Transaction = trans;
            cmdCheck.CommandText = @"
                SELECT SUM(CASE WHEN Tur IN ('Giris', 'Giriş') THEN Miktar ELSE -Miktar END)
                FROM StokHareketleri 
                WHERE StokKartId=$cid AND Id NOT IN (" + idList + ")";
            cmdCheck.Parameters.AddWithValue("$cid", cardId);
            var result = cmdCheck.ExecuteScalar();
            double balance = (result == null || result == DBNull.Value) ? 0 : Convert.ToDouble(result);
            if (balance < 0) throw new InvalidOperationException("Silme işlemi stok dengesini bozuyor (negatif stok).");
        }
    }

    private void ValidateUpdate(StokHareketi m, SqliteConnection conn)
    {
        // Güncelleme sonrası stok dengesini kontrol et
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT SUM(CASE WHEN Id=$mid THEN 0 ELSE (CASE WHEN Tur IN ('Giris', 'Giriş') THEN Miktar ELSE -Miktar END) END)
            FROM StokHareketleri WHERE StokKartId=$cid";
        cmd.Parameters.AddWithValue("$mid", m.Id);
        cmd.Parameters.AddWithValue("$cid", m.StokKartId);
        
        var baseResult = cmd.ExecuteScalar();
        double baseBalance = (baseResult == null || baseResult == DBNull.Value) ? 0 : Convert.ToDouble(baseResult);
        double newImpact = (m.Tur == "Giris") ? m.Miktar : (m.Tur == "Cikis" ? -m.Miktar : 0);
        
        if (baseBalance + newImpact < 0) throw new InvalidOperationException("Güncelleme işlemi stok dengesini bozuyor (negatif stok).");
    }
}
