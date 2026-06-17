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
    private const string StockBalancesCte = """
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
        """;

    public List<StockCard> GetAll() => GetStockCards(null, null, null);
    public async Task<List<StockCard>> GetAllAsync(CancellationToken cancellationToken = default) => await GetStockCardsAsync(null, null, null, cancellationToken);
    public List<StockCard> GetParentCards() => GetStockCards("Parent", null, null);
    public async Task<List<StockCard>> GetParentCardsAsync(CancellationToken cancellationToken = default) => await GetStockCardsAsync("Parent", null, null, cancellationToken);
    public List<StockCard> GetChildCards(int? parentId = null) => GetStockCards("Child", parentId, null);
    public async Task<List<StockCard>> GetChildCardsAsync(int? parentId = null, CancellationToken cancellationToken = default) => await GetStockCardsAsync("Child", parentId, null, cancellationToken);

    public async Task<List<StockCard>> GetLowStockCardsAsync(int limit, int fallbackThreshold = 3, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var sql = StockBalancesCte + """

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

    public async Task<StockCard?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var list = await GetStockCardsAsync(null, null, id, cancellationToken);
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

    public async Task UpdateAsync(StockCard stockCard, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try 
        {
            ValidateCard(stockCard, conn, (SqliteTransaction)trans);
            var sql = """
                UPDATE StockCards SET 
                    Code=@Code, Name=@Name, CardType=@CardType, ParentId=@ParentId, Category=@Category, 
                    Unit=@Unit, MinStock=@MinStock, Location=@Location, Supplier=@Supplier, Barcode=@Barcode, 
                    UnitPrice=@UnitPrice, Description=@Description 
                WHERE Id=@Id
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, stockCard, transaction: trans, cancellationToken: cancellationToken));
            await trans.CommitAsync(cancellationToken);
            LogAudit("Update", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
        } 
        catch 
        { 
            await trans.RollbackAsync(cancellationToken); 
            throw; 
        }
    }

    public void Delete(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try 
        {
            var hasChildren = conn.ExecuteScalar<bool>(
                "SELECT EXISTS(SELECT 1 FROM StockCards WHERE ParentId=@Id)",
                new { Id = id },
                transaction: trans);
            if (hasChildren)
                throw new InvalidOperationException(LocalizationManager.L("parent_card_has_children"));

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

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try 
        {
            var hasChildren = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM StockCards WHERE ParentId=@Id)",
                new { Id = id },
                transaction: trans,
                cancellationToken: cancellationToken));
            if (hasChildren)
                throw new InvalidOperationException(LocalizationManager.L("parent_card_has_children"));

            await conn.ExecuteAsync(new CommandDefinition("DELETE FROM StockCards WHERE Id=@Id", new { Id = id }, transaction: trans, cancellationToken: cancellationToken));
            await trans.CommitAsync(cancellationToken);
            LogAudit("Delete", "StockCards", id, "");
        } 
        catch 
        { 
            await trans.RollbackAsync(cancellationToken); 
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

    public void AddBulk(IEnumerable<StockCard> stockCards)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try
        {
            var sql = """
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Location, Supplier, Barcode, UnitPrice, Description)
                VALUES (@Code, @Name, @CardType, @ParentId, @Category, @Unit, @MinStock, @Location, @Supplier, @Barcode, @UnitPrice, @Description);
                SELECT last_insert_rowid();
                """;
            foreach (var card in stockCards)
            {
                ValidateCard(card, conn, trans);
                card.Id = conn.ExecuteScalar<int>(sql, card, transaction: trans);
                LogAudit("Insert", "StockCards", card.Id, $"Code: {card.Code}, Name: {card.Name}");
            }
            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task AddBulkAsync(IEnumerable<StockCard> stockCards, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            var sql = """
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Location, Supplier, Barcode, UnitPrice, Description)
                VALUES (@Code, @Name, @CardType, @ParentId, @Category, @Unit, @MinStock, @Location, @Supplier, @Barcode, @UnitPrice, @Description);
                SELECT last_insert_rowid();
                """;
            foreach (var card in stockCards)
            {
                ValidateCard(card, conn, (SqliteTransaction)trans);
                card.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    sql, card, transaction: trans, cancellationToken: cancellationToken));
                LogAudit("Insert", "StockCards", card.Id, $"Code: {card.Code}, Name: {card.Name}");
            }
            await trans.CommitAsync(cancellationToken);
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
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

        var sql = StockBalancesCte + $"\nSELECT *, (TotalEntry - TotalExit) as CurrentStock FROM StockBalances s WHERE {where} ORDER BY s.Code";
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

        var sql = StockBalancesCte + $"\nSELECT *, (TotalEntry - TotalExit) as CurrentStock FROM StockBalances s WHERE {where} ORDER BY s.Code";

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
            where += " AND s.CardType = @CardType";
            @params.Add("CardType", cardType);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (s.Name LIKE @Search OR s.Code LIKE @Search OR s.Category LIKE @Search OR u.Name LIKE @Search)";
            @params.Add("Search", $"%{searchTerm}%");
        }
        
        var sql = $"""
            SELECT 
                s.Id, s.Code, s.Name, s.CardType, s.ParentId, s.Category, s.Unit, s.MinStock, s.Location, s.Supplier, s.Barcode, s.UnitPrice, s.Description,
                u.Name as ParentName,
                CAST(COALESCE(entry.TotalEntry, 0) AS REAL) as TotalEntry,
                CAST(COALESCE(exit.TotalExit, 0) AS REAL) as TotalExit,
                CAST(COALESCE(entry.TotalEntry, 0) - COALESCE(exit.TotalExit, 0) AS REAL) as CurrentStock
            FROM (
                SELECT s.Id
                FROM StockCards s
                LEFT JOIN StockCards u ON s.ParentId = u.Id
                WHERE {where}
                ORDER BY s.Code ASC
                LIMIT @Limit OFFSET @Offset
            ) paged
            INNER JOIN StockCards s ON paged.Id = s.Id
            LEFT JOIN StockCards u ON s.ParentId = u.Id
            LEFT JOIN (
                SELECT StockCardId, SUM(Quantity) as TotalEntry
                FROM StockMovements
                WHERE Type = 'Entry'
                GROUP BY StockCardId
            ) entry ON s.Id = entry.StockCardId
            LEFT JOIN (
                SELECT StockCardId, SUM(Quantity) as TotalExit
                FROM StockMovements
                WHERE Type = 'Exit'
                GROUP BY StockCardId
            ) exit ON s.Id = exit.StockCardId
            ORDER BY s.Code ASC
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
        
        string sql = "SELECT COUNT(*) FROM StockCards s";
        string where = "1=1";
        
        if (!string.IsNullOrEmpty(cardType))
        {
            where += " AND s.CardType = @CardType";
            @params.Add("CardType", cardType);
        }
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql += " LEFT JOIN StockCards u ON s.ParentId = u.Id";
            where += " AND (s.Name LIKE @Search OR s.Code LIKE @Search OR s.Category LIKE @Search OR u.Name LIKE @Search)";
            @params.Add("Search", $"%{searchTerm}%");
        }
        
        sql = $"{sql} WHERE {where}";
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, @params, cancellationToken: cancellationToken));
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
            throw new InvalidOperationException(LocalizationManager.L("stock_code_exists"));

        // Hierarchical validations
        if (s.ParentId.HasValue)
        {
            if (s.ParentId == s.Id)
                throw new InvalidOperationException(LocalizationManager.L("parent_card_self"));

            // Check if parent card exists and is indeed a Parent card
            var parentType = conn.ExecuteScalar<string>(
                "SELECT CardType FROM StockCards WHERE Id=@ParentId",
                new { ParentId = s.ParentId.Value },
                transaction: trans);

            if (parentType == null)
                throw new InvalidOperationException(LocalizationManager.L("parent_card_not_found"));

            // Cycle detection
            int? currentParentId = s.ParentId;
            var visited = new System.Collections.Generic.HashSet<int> { s.Id };
            while (currentParentId.HasValue)
            {
                if (visited.Contains(currentParentId.Value))
                    throw new InvalidOperationException(LocalizationManager.L("parent_card_loop"));
                visited.Add(currentParentId.Value);

                currentParentId = conn.ExecuteScalar<int?>(
                    "SELECT ParentId FROM StockCards WHERE Id=@Id",
                    new { Id = currentParentId.Value },
                    transaction: trans);
            }
        }

        // Parent card with movements check
        if (s.CardTypeEnum == CardType.Parent)
        {
            var hasMovements = conn.ExecuteScalar<bool>(
                "SELECT EXISTS(SELECT 1 FROM StockMovements WHERE StockCardId=@Id)",
                new { Id = s.Id },
                transaction: trans);
            if (hasMovements)
                throw new InvalidOperationException(LocalizationManager.L("parent_card_with_movements"));
        }
    }
}
