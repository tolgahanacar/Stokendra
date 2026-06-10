using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Repositories;

public sealed class StockCardRepository : RepositoryBase, IStockCardRepository
{
    public StockCardRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public List<StockCard> GetAll() => GetStockCards(null, null, null);
    public async Task<List<StockCard>> GetAllAsync(CancellationToken cancellationToken = default) => await GetStockCardsAsync(null, null, null, cancellationToken);
    public List<StockCard> GetParentCards() => GetStockCards("Parent", null, null);
    public async Task<List<StockCard>> GetParentCardsAsync(CancellationToken cancellationToken = default) => await GetStockCardsAsync("Parent", null, null, cancellationToken);
    public List<StockCard> GetChildCards(int? parentId = null) => GetStockCards("Child", parentId, null);
    public async Task<List<StockCard>> GetChildCardsAsync(int? parentId = null, CancellationToken cancellationToken = default) => await GetStockCardsAsync("Child", parentId, null, cancellationToken);

    public async Task<List<StockCard>> GetLowStockCardsAsync(int limit, int fallbackThreshold = 3, CancellationToken cancellationToken = default)
    {
        var list = new List<StockCard>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.*, u.Name as ParentName,
            (SELECT COALESCE(SUM(h.Quantity), 0) FROM StockMovements h WHERE h.StockCardId = s.Id AND h.Type = 'Entry') as TotalEntry,
            (SELECT COALESCE(SUM(h.Quantity), 0) FROM StockMovements h WHERE h.StockCardId = s.Id AND h.Type = 'Exit') as TotalExit
            FROM StockCards s
            LEFT JOIN StockCards u ON s.ParentId = u.Id
            WHERE s.CardType = 'Child'
              AND ((SELECT COALESCE(SUM(h.Quantity), 0) FROM StockMovements h WHERE h.StockCardId = s.Id AND h.Type = 'Entry') - 
                   (SELECT COALESCE(SUM(h.Quantity), 0) FROM StockMovements h WHERE h.StockCardId = s.Id AND h.Type = 'Exit')) <= CASE WHEN s.MinStock > 0 THEN s.MinStock ELSE $fallback END
            ORDER BY ((SELECT COALESCE(SUM(h.Quantity), 0) FROM StockMovements h WHERE h.StockCardId = s.Id AND h.Type = 'Entry') - 
                      (SELECT COALESCE(SUM(h.Quantity), 0) FROM StockMovements h WHERE h.StockCardId = s.Id AND h.Type = 'Exit')) ASC, s.Code ASC
            LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$fallback", fallbackThreshold);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) list.Add(Read(reader));
        return list;
    }

    public StockCard? GetById(int id)
    {
        var list = GetStockCards(null, null, id);
        return list.Count > 0 ? list[0] : null;
    }

    public void Add(StockCard stockCard)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            ValidateCard(stockCard, conn, trans);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = @"
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Location, Supplier, Barcode, UnitPrice, Description)
                VALUES ($kn, $ad, $kt, $uk, $ka, $bi, $ms, $ko, $te, $ba, $bf, $ac)";
            BindParams(cmd, stockCard);
            cmd.ExecuteNonQuery();
            stockCard.Id = GetLastId(conn, trans);
            trans.Commit();
            LogAudit("Insert", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
        } catch { trans.Rollback(); throw; }
    }

    public async Task AddAsync(StockCard stockCard, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try {
            ValidateCard(stockCard, conn, (SqliteTransaction)trans);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)trans;
            cmd.CommandText = @"
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Location, Supplier, Barcode, UnitPrice, Description)
                VALUES ($kn, $ad, $kt, $uk, $ka, $bi, $ms, $ko, $te, $ba, $bf, $ac)";
            BindParams(cmd, stockCard);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            stockCard.Id = GetLastId(conn, (SqliteTransaction)trans);
            await trans.CommitAsync(cancellationToken);
            LogAudit("Insert", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
        } catch { await trans.RollbackAsync(cancellationToken); throw; }
    }

    public void Update(StockCard stockCard)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            ValidateCard(stockCard, conn, trans);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = @"
                UPDATE StockCards SET 
                    Code=$kn, Name=$ad, CardType=$kt, ParentId=$uk, Category=$ka, 
                    Unit=$bi, MinStock=$ms, Location=$ko, Supplier=$te, Barcode=$ba, 
                    UnitPrice=$bf, Description=$ac 
                WHERE Id=$id";
            BindParams(cmd, stockCard);
            cmd.Parameters.AddWithValue("$id", stockCard.Id);
            cmd.ExecuteNonQuery();
            trans.Commit();
            LogAudit("Update", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
        } catch { trans.Rollback(); throw; }
    }

    public void Delete(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "DELETE FROM StockCards WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            trans.Commit();
            LogAudit("Delete", "StockCards", id, "");
        } catch { trans.Rollback(); throw; }
    }

    public string GetNextCode()
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(CAST(Code AS INTEGER)) FROM StockCards WHERE Code GLOB '[0-9]*'";
        var result = cmd.ExecuteScalar();
        int max = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
        return (max + 1).ToString("D3", CultureInfo.InvariantCulture);
    }

    private List<StockCard> GetStockCards(string? type, int? parentId, int? id)
    {
        var list = new List<StockCard>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(type, parentId, id);
        if (type != null) cmd.Parameters.AddWithValue("$kt", type);
        if (parentId.HasValue) cmd.Parameters.AddWithValue("$uk", parentId.Value);
        if (id.HasValue) cmd.Parameters.AddWithValue("$id", id.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(Read(reader));
        return list;
    }

    private async Task<List<StockCard>> GetStockCardsAsync(string? type, int? parentId, int? id, CancellationToken cancellationToken = default)
    {
        var list = new List<StockCard>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(type, parentId, id);
        if (type != null) cmd.Parameters.AddWithValue("$kt", type);
        if (parentId.HasValue) cmd.Parameters.AddWithValue("$uk", parentId.Value);
        if (id.HasValue) cmd.Parameters.AddWithValue("$id", id.Value);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) list.Add(Read(reader));
        return list;
    }

    private string BuildQuery(string? type, int? parentId, int? id)
    {
        var sql = @"
            SELECT s.*, u.Name as ParentName,
            (SELECT COALESCE(SUM(h.Quantity), 0) FROM StockMovements h WHERE h.StockCardId = s.Id AND h.Type = 'Entry') as TotalEntry,
            (SELECT COALESCE(SUM(h.Quantity), 0) FROM StockMovements h WHERE h.StockCardId = s.Id AND h.Type = 'Exit') as TotalExit
            FROM StockCards s
            LEFT JOIN StockCards u ON s.ParentId = u.Id
            WHERE 1=1";
        
        if (type != null) sql += " AND s.CardType = $kt";
        if (parentId.HasValue) sql += " AND s.ParentId = $uk";
        if (id.HasValue) sql += " AND s.Id = $id";
        
        sql += " ORDER BY s.Code";
        return sql;
    }

    private StockCard Read(SqliteDataReader reader)
    {
        var result = new StockCard {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Code = reader.GetString(reader.GetOrdinal("Code")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            CardType = reader.GetString(reader.GetOrdinal("CardType")),
            ParentId = reader.IsDBNull(reader.GetOrdinal("ParentId")) ? null : reader.GetInt32(reader.GetOrdinal("ParentId")),
            ParentName = reader.IsDBNull(reader.GetOrdinal("ParentName")) ? "" : reader.GetString(reader.GetOrdinal("ParentName")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "" : reader.GetString(reader.GetOrdinal("Category")),
            Unit = reader.IsDBNull(reader.GetOrdinal("Unit")) ? "" : reader.GetString(reader.GetOrdinal("Unit")),
            MinStock = reader.GetInt32(reader.GetOrdinal("MinStock")),
            Location = reader.IsDBNull(reader.GetOrdinal("Location")) ? "" : reader.GetString(reader.GetOrdinal("Location")),
            Supplier = reader.IsDBNull(reader.GetOrdinal("Supplier")) ? "" : reader.GetString(reader.GetOrdinal("Supplier")),
            Barcode = reader.IsDBNull(reader.GetOrdinal("Barcode")) ? "" : reader.GetString(reader.GetOrdinal("Barcode")),
            UnitPrice = reader.GetDouble(reader.GetOrdinal("UnitPrice")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
            TotalEntry = reader.IsDBNull(reader.GetOrdinal("TotalEntry")) ? 0 : reader.GetDouble(reader.GetOrdinal("TotalEntry")),
            TotalExit = reader.IsDBNull(reader.GetOrdinal("TotalExit")) ? 0 : reader.GetDouble(reader.GetOrdinal("TotalExit"))
        };
        result.CurrentStock = result.TotalEntry - result.TotalExit;
        return result;
    }

    private void BindParams(SqliteCommand cmd, StockCard s)
    {
        cmd.Parameters.AddWithValue("$kn", s.Code);
        cmd.Parameters.AddWithValue("$ad", s.Name);
        cmd.Parameters.AddWithValue("$kt", s.CardType);
        cmd.Parameters.AddWithValue("$uk", (object?)s.ParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ka", s.Category ?? "");
        cmd.Parameters.AddWithValue("$bi", s.Unit ?? "");
        cmd.Parameters.AddWithValue("$ms", s.MinStock);
        cmd.Parameters.AddWithValue("$ko", s.Location ?? "");
        cmd.Parameters.AddWithValue("$te", s.Supplier ?? "");
        cmd.Parameters.AddWithValue("$ba", s.Barcode ?? "");
        cmd.Parameters.AddWithValue("$bf", s.UnitPrice);
        cmd.Parameters.AddWithValue("$ac", s.Description ?? "");
    }

    private int GetLastId(SqliteConnection conn, SqliteTransaction? trans = null)
    {
        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        cmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void ValidateCard(StockCard s, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) throw new InvalidOperationException("Stock name cannot be empty.");
        if (string.IsNullOrWhiteSpace(s.Code)) throw new InvalidOperationException("Stock code cannot be empty.");
        
        if (s.MinStock < 0) s.MinStock = 0;
        if (s.UnitPrice < 0) s.UnitPrice = 0;

        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        cmd.CommandText = "SELECT COUNT(*) FROM StockCards WHERE Code=$kn AND Id<>$id";
        cmd.Parameters.AddWithValue("$kn", s.Code);
        cmd.Parameters.AddWithValue("$id", s.Id);
        if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
            throw new InvalidOperationException("This stock code is already in use.");
    }
}
