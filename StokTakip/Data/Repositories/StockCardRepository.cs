using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Globalization;

namespace StokTakip.Data.Repositories;

public sealed class StockCardRepository : IStockCardRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StockCardRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<StokKarti> GetAll() => GetStockCards(null, null, null);
    public async Task<List<StokKarti>> GetAllAsync() => await GetStockCardsAsync(null, null, null);
    public List<StokKarti> GetParentCards() => GetStockCards("Ust", null, null);
    public async Task<List<StokKarti>> GetParentCardsAsync() => await GetStockCardsAsync("Ust", null, null);
    public List<StokKarti> GetChildCards(int? parentId = null) => GetStockCards("Alt", parentId, null);
    public async Task<List<StokKarti>> GetChildCardsAsync(int? parentId = null) => await GetStockCardsAsync("Alt", parentId, null);

    public StokKarti? GetById(int id)
    {
        var list = GetStockCards(null, null, id);
        return list.Count > 0 ? list[0] : null;
    }

    public void Add(StokKarti stokKarti)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO StokKartlari (KodNo, Ad, KartTipi, UstKartId, Kategori, Birim, MinStok, Konum, Tedarikci, Barkod, BirimFiyat, Aciklama)
            VALUES ($kn, $ad, $kt, $uk, $ka, $bi, $ms, $ko, $te, $ba, $bf, $ac)";
        BindParams(cmd, stokKarti);
        cmd.ExecuteNonQuery();
        stokKarti.Id = GetLastId(conn);
    }

    public async Task AddAsync(StokKarti stokKarti)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO StokKartlari (KodNo, Ad, KartTipi, UstKartId, Kategori, Birim, MinStok, Konum, Tedarikci, Barkod, BirimFiyat, Aciklama)
            VALUES ($kn, $ad, $kt, $uk, $ka, $bi, $ms, $ko, $te, $ba, $bf, $ac)";
        BindParams(cmd, stokKarti);
        await cmd.ExecuteNonQueryAsync();
        stokKarti.Id = GetLastId(conn);
    }

    public void Update(StokKarti stokKarti)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE StokKartlari SET 
                KodNo=$kn, Ad=$ad, KartTipi=$kt, UstKartId=$uk, Kategori=$ka, 
                Birim=$bi, MinStok=$ms, Konum=$ko, Tedarikci=$te, Barkod=$ba, 
                BirimFiyat=$bf, Aciklama=$ac 
            WHERE Id=$id";
        BindParams(cmd, stokKarti);
        cmd.Parameters.AddWithValue("$id", stokKarti.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM StokKartlari WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public string GetNextCode()
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(CAST(KodNo AS INTEGER)) FROM StokKartlari WHERE KodNo GLOB '[0-9]*'";
        var result = cmd.ExecuteScalar();
        int max = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
        return (max + 1).ToString("D3", CultureInfo.InvariantCulture);
    }

    private List<StokKarti> GetStockCards(string? type, int? parentId, int? id)
    {
        var list = new List<StokKarti>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(type, parentId, id);
        if (type != null) cmd.Parameters.AddWithValue("$kt", type);
        if (parentId.HasValue) cmd.Parameters.AddWithValue("$uk", parentId.Value);
        if (id.HasValue) cmd.Parameters.AddWithValue("$id", id.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(Read(reader));
        return list;
    }

    private async Task<List<StokKarti>> GetStockCardsAsync(string? type, int? parentId, int? id)
    {
        var list = new List<StokKarti>();
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(type, parentId, id);
        if (type != null) cmd.Parameters.AddWithValue("$kt", type);
        if (parentId.HasValue) cmd.Parameters.AddWithValue("$uk", parentId.Value);
        if (id.HasValue) cmd.Parameters.AddWithValue("$id", id.Value);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(Read(reader));
        return list;
    }

    private string BuildQuery(string? type, int? parentId, int? id)
    {
        var sql = @"
            SELECT s.*, u.Ad as UstKartAd,
            (SELECT SUM(CASE WHEN Tur IN ('Giris', 'Giriş') THEN Miktar ELSE -Miktar END) FROM StokHareketleri WHERE StokKartId=s.Id) as MevcutStok
            FROM StokKartlari s
            LEFT JOIN StokKartlari u ON s.UstKartId = u.Id
            WHERE 1=1";
        if (type != null) sql += " AND s.KartTipi = $kt";
        if (parentId.HasValue) sql += " AND s.UstKartId = $uk";
        if (id.HasValue) sql += " AND s.Id = $id";
        sql += " ORDER BY s.KodNo";
        return sql;
    }

    private StokKarti Read(SqliteDataReader reader)
    {
        return new StokKarti {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            KodNo = reader.GetString(reader.GetOrdinal("KodNo")),
            Ad = reader.GetString(reader.GetOrdinal("Ad")),
            KartTipi = reader.GetString(reader.GetOrdinal("KartTipi")),
            UstKartId = reader.IsDBNull(reader.GetOrdinal("UstKartId")) ? null : reader.GetInt32(reader.GetOrdinal("UstKartId")),
            UstKartAd = reader.IsDBNull(reader.GetOrdinal("UstKartAd")) ? "" : reader.GetString(reader.GetOrdinal("UstKartAd")),
            Kategori = reader.IsDBNull(reader.GetOrdinal("Kategori")) ? "" : reader.GetString(reader.GetOrdinal("Kategori")),
            Birim = reader.IsDBNull(reader.GetOrdinal("Birim")) ? "" : reader.GetString(reader.GetOrdinal("Birim")),
            MinStok = reader.GetInt32(reader.GetOrdinal("MinStok")),
            Konum = reader.IsDBNull(reader.GetOrdinal("Konum")) ? "" : reader.GetString(reader.GetOrdinal("Konum")),
            Tedarikci = reader.IsDBNull(reader.GetOrdinal("Tedarikci")) ? "" : reader.GetString(reader.GetOrdinal("Tedarikci")),
            Barkod = reader.IsDBNull(reader.GetOrdinal("Barkod")) ? "" : reader.GetString(reader.GetOrdinal("Barkod")),
            BirimFiyat = reader.GetDouble(reader.GetOrdinal("BirimFiyat")),
            Aciklama = reader.IsDBNull(reader.GetOrdinal("Aciklama")) ? "" : reader.GetString(reader.GetOrdinal("Aciklama")),
            MevcutStok = reader.IsDBNull(reader.GetOrdinal("MevcutStok")) ? 0 : reader.GetDouble(reader.GetOrdinal("MevcutStok"))
        };
    }

    private void BindParams(SqliteCommand cmd, StokKarti s)
    {
        cmd.Parameters.AddWithValue("$kn", s.KodNo);
        cmd.Parameters.AddWithValue("$ad", s.Ad);
        cmd.Parameters.AddWithValue("$kt", s.KartTipi);
        cmd.Parameters.AddWithValue("$uk", (object?)s.UstKartId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ka", s.Kategori ?? "");
        cmd.Parameters.AddWithValue("$bi", s.Birim ?? "");
        cmd.Parameters.AddWithValue("$ms", s.MinStok);
        cmd.Parameters.AddWithValue("$ko", s.Konum ?? "");
        cmd.Parameters.AddWithValue("$te", s.Tedarikci ?? "");
        cmd.Parameters.AddWithValue("$ba", s.Barkod ?? "");
        cmd.Parameters.AddWithValue("$bf", s.BirimFiyat);
        cmd.Parameters.AddWithValue("$ac", s.Aciklama ?? "");
    }

    private int GetLastId(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
