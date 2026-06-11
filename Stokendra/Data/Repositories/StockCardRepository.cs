using Dapper;
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

public sealed class StockCardRepository(IDbConnectionFactory connectionFactory) 
    : RepositoryBase(connectionFactory), IStockCardRepository
{
    public List<StockCard> GetAll() => GetStockCards(null, null, null);
    public async Task<List<StockCard>> GetAllAsync(CancellationToken cancellationToken = default) => await GetStockCardsAsync(null, null, null, cancellationToken);
    public List<StockCard> GetParentCards() => GetStockCards("Parent", null, null);
    public async Task<List<StockCard>> GetParentCardsAsync(CancellationToken cancellationToken = default) => await GetStockCardsAsync("Parent", null, null, cancellationToken);
    public List<StockCard> GetChildCards(int? parentId = null) => GetStockCards("Child", parentId, null);
    public async Task<List<StockCard>> GetChildCardsAsync(int? parentId = null, CancellationToken cancellationToken = default) => await GetStockCardsAsync("Child", parentId, null, cancellationToken);

    public async Task<List<StockCard>> GetLowStockCardsAsync(int limit, int fallbackThreshold = 3, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var sql = """
            WITH StockBalances AS (
                SELECT s.Id, s.Code, s.Name, s.CardType, s.ParentId, s.Category, s.Unit, s.MinStock, s.Location, s.Supplier, s.Barcode, s.UnitPrice, s.Description,
                       u.Name as ParentName,
                       CAST(COALESCE(SUM(CASE WHEN h.Type = 'Entry' THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalEntry,
                       CAST(COALESCE(SUM(CASE WHEN h.Type = 'Exit' THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalExit
                FROM StockCards s
                LEFT JOIN StockCards u ON s.ParentId = u.Id
                LEFT JOIN StockMovements h ON h.StockCardId = s.Id
                WHERE s.CardType = 'Child'
                GROUP BY s.Id
            )
            SELECT *, (TotalEntry - TotalExit) as CurrentStock
            FROM StockBalances
            WHERE CurrentStock <= CASE WHEN MinStock > 0 THEN MinStock ELSE @Fallback END
            ORDER BY CurrentStock ASC, Code ASC
            LIMIT @Limit
            """;

        var result = await conn.QueryAsync<StockCard>(new CommandDefinition(
            sql, 
            new { Limit = limit, Fallback = fallbackThreshold }, 
            cancellationToken: cancellationToken));
        return result.ToList();
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
        try 
        {
            ValidateCard(stockCard, conn, trans);
            var sql = """
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Location, Supplier, Barcode, UnitPrice, Description)
                VALUES (@Code, @Name, @CardType, @ParentId, @Category, @Unit, @MinStock, @Location, @Supplier, @Barcode, @UnitPrice, @Description);
                SELECT last_insert_rowid();
                """;
            stockCard.Id = conn.ExecuteScalar<int>(sql, stockCard, transaction: trans);
            trans.Commit();
            LogAudit("Insert", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
        } 
        catch 
        { 
            trans.Rollback(); 
            throw; 
        }
    }

    public async Task AddAsync(StockCard stockCard, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try 
        {
            ValidateCard(stockCard, conn, (SqliteTransaction)trans);
            var sql = """
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Location, Supplier, Barcode, UnitPrice, Description)
                VALUES (@Code, @Name, @CardType, @ParentId, @Category, @Unit, @MinStock, @Location, @Supplier, @Barcode, @UnitPrice, @Description);
                SELECT last_insert_rowid();
                """;
            stockCard.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                sql, stockCard, transaction: trans, cancellationToken: cancellationToken));
            await trans.CommitAsync(cancellationToken);
            LogAudit("Insert", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
        } 
        catch 
        { 
            await trans.RollbackAsync(cancellationToken); 
            throw; 
        }
    }

    public void Update(StockCard stockCard)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try 
        {
            ValidateCard(stockCard, conn, trans);
            var sql = """
                UPDATE StockCards SET 
                    Code=@Code, Name=@Name, CardType=@CardType, ParentId=@ParentId, Category=@Category, 
                    Unit=@Unit, MinStock=@MinStock, Location=@Location, Supplier=@Supplier, Barcode=@Barcode, 
                    UnitPrice=@UnitPrice, Description=@Description 
                WHERE Id=@Id
                """;
            conn.Execute(sql, stockCard, transaction: trans);
            trans.Commit();
            LogAudit("Update", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
        } 
        catch 
        { 
            trans.Rollback(); 
            throw; 
        }
    }

    public void Delete(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try 
        {
            conn.Execute("DELETE FROM StockCards WHERE Id=@Id", new { Id = id }, transaction: trans);
            trans.Commit();
            LogAudit("Delete", "StockCards", id, "");
        } 
        catch 
        { 
            trans.Rollback(); 
            throw; 
        }
    }

    public string GetNextCode()
    {
        using var conn = ConnectionFactory.CreateConnection();
        var result = conn.ExecuteScalar("SELECT MAX(CAST(Code AS INTEGER)) FROM StockCards WHERE Code GLOB '[0-9]*'");
        int max = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
        return (max + 1).ToString("D3", CultureInfo.InvariantCulture);
    }

    private List<StockCard> GetStockCards(string? type, int? parentId, int? id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        string where = "1=1";
        if (type != null)
        {
            where += " AND s.CardType = @CardType";
            @params.Add("CardType", type);
        }
        if (parentId.HasValue)
        {
            where += " AND s.ParentId = @ParentId";
            @params.Add("ParentId", parentId.Value);
        }
        if (id.HasValue)
        {
            where += " AND s.Id = @Id";
            @params.Add("Id", id.Value);
        }

        var sql = $"""
            WITH StockBalances AS (
                SELECT s.Id, s.Code, s.Name, s.CardType, s.ParentId, s.Category, s.Unit, s.MinStock, s.Location, s.Supplier, s.Barcode, s.UnitPrice, s.Description,
                       u.Name as ParentName,
                       CAST(COALESCE(SUM(CASE WHEN h.Type = 'Entry' THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalEntry,
                       CAST(COALESCE(SUM(CASE WHEN h.Type = 'Exit' THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalExit
                FROM StockCards s
                LEFT JOIN StockCards u ON s.ParentId = u.Id
                LEFT JOIN StockMovements h ON h.StockCardId = s.Id
                GROUP BY s.Id
            )
            SELECT *, (TotalEntry - TotalExit) as CurrentStock
            FROM StockBalances s
            WHERE {where}
            ORDER BY s.Code
            """;

        return conn.Query<StockCard>(sql, @params).ToList();
    }

    private async Task<List<StockCard>> GetStockCardsAsync(string? type, int? parentId, int? id, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        string where = "1=1";
        if (type != null)
        {
            where += " AND s.CardType = @CardType";
            @params.Add("CardType", type);
        }
        if (parentId.HasValue)
        {
            where += " AND s.ParentId = @ParentId";
            @params.Add("ParentId", parentId.Value);
        }
        if (id.HasValue)
        {
            where += " AND s.Id = @Id";
            @params.Add("Id", id.Value);
        }

        var sql = $"""
            WITH StockBalances AS (
                SELECT s.Id, s.Code, s.Name, s.CardType, s.ParentId, s.Category, s.Unit, s.MinStock, s.Location, s.Supplier, s.Barcode, s.UnitPrice, s.Description,
                       u.Name as ParentName,
                       CAST(COALESCE(SUM(CASE WHEN h.Type = 'Entry' THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalEntry,
                       CAST(COALESCE(SUM(CASE WHEN h.Type = 'Exit' THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalExit
                FROM StockCards s
                LEFT JOIN StockCards u ON s.ParentId = u.Id
                LEFT JOIN StockMovements h ON h.StockCardId = s.Id
                GROUP BY s.Id
            )
            SELECT *, (TotalEntry - TotalExit) as CurrentStock
            FROM StockBalances s
            WHERE {where}
            ORDER BY s.Code
            """;

        var result = await conn.QueryAsync<StockCard>(new CommandDefinition(
            sql, @params, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<List<StockCard>> GetPagedAsync(int page, int pageSize, string? searchTerm, string? cardType = null, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        
        string where = "1=1";
        if (!string.IsNullOrEmpty(cardType))
        {
            where += " AND CardType = @CardType";
            @params.Add("CardType", cardType);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (Name LIKE @Search OR Code LIKE @Search OR Category LIKE @Search OR ParentName LIKE @Search)";
            @params.Add("Search", $"%{searchTerm}%");
        }
        
        var sql = $"""
            WITH StockBalances AS (
                SELECT s.Id, s.Code, s.Name, s.CardType, s.ParentId, s.Category, s.Unit, s.MinStock, s.Location, s.Supplier, s.Barcode, s.UnitPrice, s.Description,
                       u.Name as ParentName,
                       CAST(COALESCE(SUM(CASE WHEN h.Type = 'Entry' THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalEntry,
                       CAST(COALESCE(SUM(CASE WHEN h.Type = 'Exit' THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalExit
                FROM StockCards s
                LEFT JOIN StockCards u ON s.ParentId = u.Id
                LEFT JOIN StockMovements h ON h.StockCardId = s.Id
                GROUP BY s.Id
            )
            SELECT *, (TotalEntry - TotalExit) as CurrentStock
            FROM StockBalances
            WHERE {where}
            ORDER BY Code ASC
            LIMIT @Limit OFFSET @Offset
            """;
            
        @params.Add("Limit", pageSize);
        @params.Add("Offset", (page - 1) * pageSize);

        var result = await conn.QueryAsync<StockCard>(new CommandDefinition(
            sql, @params, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<int> GetCountAsync(string? searchTerm, string? cardType = null, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        
        string where = "1=1";
        if (!string.IsNullOrEmpty(cardType))
        {
            where += " AND s.CardType = @CardType";
            @params.Add("CardType", cardType);
        }
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var sql = """
                SELECT COUNT(*) FROM StockCards s
                LEFT JOIN StockCards u ON s.ParentId = u.Id
                WHERE 1=1
                """;
            if (!string.IsNullOrEmpty(cardType))
                sql += " AND s.CardType = @CardType";
            sql += " AND (s.Name LIKE @Search OR s.Code LIKE @Search OR s.Category LIKE @Search OR u.Name LIKE @Search)";
            @params.Add("Search", $"%{searchTerm}%");
            @params.Add("CardType", cardType);
            return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, @params, cancellationToken: cancellationToken));
        }
        else
        {
            var sql = "SELECT COUNT(*) FROM StockCards s WHERE 1=1";
            if (!string.IsNullOrEmpty(cardType))
                sql += " AND s.CardType = @CardType";
            @params.Add("CardType", cardType);
            return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, @params, cancellationToken: cancellationToken));
        }
    }

    private void ValidateCard(StockCard s, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) throw new InvalidOperationException("Stock name cannot be empty.");
        if (string.IsNullOrWhiteSpace(s.Code)) throw new InvalidOperationException("Stock code cannot be empty.");
        
        if (s.MinStock < 0) s.MinStock = 0;
        if (s.UnitPrice < 0) s.UnitPrice = 0;

        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM StockCards WHERE Code=@Code AND Id<>@Id",
            new { Code = s.Code, Id = s.Id },
            transaction: trans);
        if (count > 0)
            throw new InvalidOperationException("This stock code is already in use.");
    }
}
