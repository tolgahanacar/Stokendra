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
            SELECT s.Id, s.Code, s.Name, s.CardType, s.ParentId, s.Category, s.Unit, s.MinStock, s.Description,
                   u.Name as ParentName,
                   CAST(COALESCE(SUM(CASE WHEN h.Type IN ('Entry', 'Giris', 'Giriş') THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalEntry,
                   CAST(COALESCE(SUM(CASE WHEN h.Type IN ('Exit', 'Cikis', 'Çıkış') THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalExit,
                   CAST(COALESCE(SUM(CASE
                       WHEN h.Type IN ('Entry', 'Giris', 'Giriş') THEN h.Quantity
                       WHEN h.Type IN ('Exit', 'Cikis', 'Çıkış') THEN -h.Quantity
                       ELSE 0
                   END), 0) AS REAL) as CurrentStock
            FROM StockCards s
            LEFT JOIN StockCards u ON s.ParentId = u.Id
            LEFT JOIN StockMovements h ON h.StockCardId = s.Id
            WHERE s.CardType = 'Child'
            GROUP BY s.Id
            HAVING CurrentStock <= CASE WHEN s.MinStock > 0 THEN s.MinStock ELSE @Fallback END
            ORDER BY CurrentStock ASC, s.Code ASC
            LIMIT @Limit
            """;

        var result = await conn.QueryAsync<StockCard>(new CommandDefinition(
            sql, 
            new { Limit = limit, Fallback = fallbackThreshold }, 
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Description)
                VALUES (@Code, @Name, @CardType, @ParentId, @Category, @Unit, @MinStock, @Description);
                SELECT last_insert_rowid();
                """;
            stockCard.Id = conn.ExecuteScalar<int>(sql, stockCard, transaction: trans);
            LogAudit(conn, trans, "Insert", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
            trans.Commit();
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
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Description)
                VALUES (@Code, @Name, @CardType, @ParentId, @Category, @Unit, @MinStock, @Description);
                SELECT last_insert_rowid();
                """;
            stockCard.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                sql, stockCard, transaction: trans, cancellationToken: cancellationToken));
            LogAudit(conn, (SqliteTransaction)trans, "Insert", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
            await trans.CommitAsync(cancellationToken);
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
                    Unit=@Unit, MinStock=@MinStock, 
                    Description=@Description 
                WHERE Id=@Id
                """;
            conn.Execute(sql, stockCard, transaction: trans);
            LogAudit(conn, trans, "Update", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
            trans.Commit();
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
                    Unit=@Unit, MinStock=@MinStock, 
                    Description=@Description 
                WHERE Id=@Id
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, stockCard, transaction: trans, cancellationToken: cancellationToken));
            LogAudit(conn, (SqliteTransaction)trans, "Update", "StockCards", stockCard.Id, $"Code: {stockCard.Code}, Name: {stockCard.Name}");
            await trans.CommitAsync(cancellationToken);
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
            LogAudit(conn, trans, "Delete", "StockCards", id, "");
            trans.Commit();
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
            LogAudit(conn, (SqliteTransaction)trans, "Delete", "StockCards", id, "");
            await trans.CommitAsync(cancellationToken);
        } 
        catch 
        { 
            await trans.RollbackAsync(cancellationToken); 
            throw; 
        }
    }

    public async Task DeleteBulkAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try 
        {
            var hasChildren = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM StockCards WHERE ParentId IN @Ids AND Id NOT IN @Ids)",
                new { Ids = idList },
                transaction: trans,
                cancellationToken: cancellationToken));
            if (hasChildren)
                throw new InvalidOperationException(LocalizationManager.L("parent_card_has_children"));

            await conn.ExecuteAsync(new CommandDefinition("DELETE FROM StockCards WHERE Id IN @Ids", new { Ids = idList }, transaction: trans, cancellationToken: cancellationToken));
            LogAudit(conn, (SqliteTransaction)trans, "Delete", "StockCards", 0, "Bulk StockCard Delete");
            await trans.CommitAsync(cancellationToken);
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
        var result = conn.ExecuteScalar("SELECT MAX(CAST(Code AS INTEGER)) FROM StockCards WHERE Code GLOB '[0-9]*' AND Code NOT GLOB '*[^0-9]*' AND LENGTH(Code) <= 9");
        long max = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0;
        if (max >= int.MaxValue) max = 0;
        return (max + 1).ToString("D3", CultureInfo.InvariantCulture);
    }

    public async Task<string> GetNextCodeAsync(CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var result = await conn.ExecuteScalarAsync<object?>(new CommandDefinition(
            "SELECT MAX(CAST(Code AS INTEGER)) FROM StockCards WHERE Code GLOB '[0-9]*' AND Code NOT GLOB '*[^0-9]*' AND LENGTH(Code) <= 9",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        long max = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0;
        if (max >= int.MaxValue) max = 0;
        return (max + 1).ToString("D3", CultureInfo.InvariantCulture);
    }

    public async Task<List<StockCard>> GetCardLookupsAsync(string? cardType = null, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        string where = string.IsNullOrEmpty(cardType) ? "" : "WHERE CardType = @CardType";
        var sql = $"SELECT Id, Code, Name, CardType, ParentId, Category, Unit, MinStock, Description FROM StockCards {where} ORDER BY Code";
        var result = await conn.QueryAsync<StockCard>(new CommandDefinition(sql, new { CardType = cardType }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result.ToList();
    }

    private static void ValidateBulkCardsForInsert(List<StockCard> list, SqliteConnection conn, SqliteTransaction trans)
    {
        var existingRows = conn.Query<(int Id, string Code, string CardType, int? ParentId)>(
            "SELECT Id, Code, CardType, ParentId FROM StockCards", transaction: trans).ToList();

        var existingCodes = new HashSet<string>(existingRows.Select(r => r.Code), StringComparer.OrdinalIgnoreCase);
        var parentMap = existingRows.ToDictionary(r => r.Id, r => (r.CardType, r.ParentId));

        foreach (var s in list)
        {
            if (string.IsNullOrWhiteSpace(s.Name)) throw new InvalidOperationException(LocalizationManager.L("name_required"));
            if (string.IsNullOrWhiteSpace(s.Code)) throw new InvalidOperationException(LocalizationManager.L("code_required"));

            s.Name = s.Name.Trim();
            s.Code = s.Code.Trim();
            s.CardType = s.CardTypeEnum.ToDbString();
            if (s.MinStock < 0) s.MinStock = 0;

            if (!existingCodes.Add(s.Code))
                throw new InvalidOperationException(LocalizationManager.L("stock_code_exists"));

            if (s.ParentId.HasValue)
            {
                if (s.ParentId == s.Id)
                    throw new InvalidOperationException(LocalizationManager.L("parent_card_self"));

                if (!parentMap.TryGetValue(s.ParentId.Value, out var parentInfo) || parentInfo.CardType.ToCardType() != CardType.Parent)
                    throw new InvalidOperationException(LocalizationManager.L("parent_card_not_found"));

                int? currId = s.ParentId;
                var visited = new HashSet<int> { s.Id };
                while (currId.HasValue)
                {
                    if (!visited.Add(currId.Value))
                        throw new InvalidOperationException(LocalizationManager.L("parent_card_loop"));
                    currId = parentMap.TryGetValue(currId.Value, out var node) ? node.ParentId : null;
                }
            }
        }
    }

    public void AddBulk(IEnumerable<StockCard> stockCards)
    {
        var list = stockCards.ToList();
        if (list.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try
        {
            ValidateBulkCardsForInsert(list, conn, trans);
            var sql = """
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Description)
                VALUES (@Code, @Name, @CardType, @ParentId, @Category, @Unit, @MinStock, @Description);
                SELECT last_insert_rowid();
                """;
            foreach (var card in list)
            {
                card.Id = conn.ExecuteScalar<int>(sql, card, transaction: trans);
                LogAudit(conn, trans, "Insert", "StockCards", card.Id, $"Code: {card.Code}, Name: {card.Name}");
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
        var list = stockCards.ToList();
        if (list.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            ValidateBulkCardsForInsert(list, conn, (SqliteTransaction)trans);
            var sql = """
                INSERT INTO StockCards (Code, Name, CardType, ParentId, Category, Unit, MinStock, Description)
                VALUES (@Code, @Name, @CardType, @ParentId, @Category, @Unit, @MinStock, @Description);
                SELECT last_insert_rowid();
                """;
            foreach (var card in list)
            {
                card.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    sql, card, transaction: trans, cancellationToken: cancellationToken));
                LogAudit(conn, (SqliteTransaction)trans, "Insert", "StockCards", card.Id, $"Code: {card.Code}, Name: {card.Name}");
            }
            await trans.CommitAsync(cancellationToken);
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string BuildStockBalancesSql(string where) => $"""
        SELECT s.Id, s.Code, s.Name, s.CardType, s.ParentId, s.Category, s.Unit, s.MinStock, s.Description,
               u.Name as ParentName,
               CAST(COALESCE(SUM(CASE WHEN h.Type IN ('Entry', 'Giris', 'Giriş') THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalEntry,
               CAST(COALESCE(SUM(CASE WHEN h.Type IN ('Exit', 'Cikis', 'Çıkış') THEN h.Quantity ELSE 0 END), 0) AS REAL) as TotalExit,
               CAST(COALESCE(SUM(CASE
                   WHEN h.Type IN ('Entry', 'Giris', 'Giriş') THEN h.Quantity
                   WHEN h.Type IN ('Exit', 'Cikis', 'Çıkış') THEN -h.Quantity
                   ELSE 0
               END), 0) AS REAL) as CurrentStock
        FROM StockCards s
        LEFT JOIN StockCards u ON s.ParentId = u.Id
        LEFT JOIN StockMovements h ON h.StockCardId = s.Id
        WHERE {where}
        GROUP BY s.Id
        ORDER BY s.Code
        """;

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

        return conn.Query<StockCard>(BuildStockBalancesSql(where), @params).ToList();
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

        var result = await conn.QueryAsync<StockCard>(new CommandDefinition(
            BuildStockBalancesSql(where), @params, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
            WITH paged AS MATERIALIZED (
                SELECT s.Id
                FROM StockCards s
                LEFT JOIN StockCards u ON s.ParentId = u.Id
                WHERE {where}
                ORDER BY s.Code ASC
                LIMIT @Limit OFFSET @Offset
            )
            SELECT 
                s.Id, s.Code, s.Name, s.CardType, s.ParentId, s.Category, s.Unit, s.MinStock, s.Description,
                u.Name as ParentName,
                CAST(COALESCE(entry.TotalEntry, 0) AS REAL) as TotalEntry,
                CAST(COALESCE(exit.TotalExit, 0) AS REAL) as TotalExit,
                CAST(COALESCE(entry.TotalEntry, 0) - COALESCE(exit.TotalExit, 0) AS REAL) as CurrentStock
            FROM paged
            INNER JOIN StockCards s ON paged.Id = s.Id
            LEFT JOIN StockCards u ON s.ParentId = u.Id
            LEFT JOIN (
                SELECT StockCardId, SUM(Quantity) as TotalEntry
                FROM StockMovements
                WHERE StockCardId IN (SELECT Id FROM paged) AND Type IN ('Entry', 'Giris', 'Giriş')
                GROUP BY StockCardId
            ) entry ON s.Id = entry.StockCardId
            LEFT JOIN (
                SELECT StockCardId, SUM(Quantity) as TotalExit
                FROM StockMovements
                WHERE StockCardId IN (SELECT Id FROM paged) AND Type IN ('Exit', 'Cikis', 'Çıkış')
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

    public async Task<(int TotalCount, int LowStockCount, int DepletedCount)> GetChildStockSummaryAsync(string? searchTerm, int fallbackThreshold = 3, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        @params.Add("Fallback", fallbackThreshold);

        string searchFilter = "";
        string parentJoin = "";
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            parentJoin = "LEFT JOIN StockCards u ON s.ParentId = u.Id";
            searchFilter = " AND (s.Name LIKE @Search OR s.Code LIKE @Search OR s.Category LIKE @Search OR u.Name LIKE @Search)";
            @params.Add("Search", $"%{searchTerm}%");
        }

        string sql = $"""
            WITH FilteredBalances AS (
                SELECT s.Id, s.MinStock,
                       COALESCE(SUM(CASE
                           WHEN h.Type IN ('Entry', 'Giris', 'Giriş') THEN h.Quantity
                           WHEN h.Type IN ('Exit', 'Cikis', 'Çıkış') THEN -h.Quantity
                           ELSE 0
                       END), 0) AS Balance
                FROM StockCards s
                {parentJoin}
                LEFT JOIN StockMovements h ON h.StockCardId = s.Id
                WHERE s.CardType = 'Child' {searchFilter}
                GROUP BY s.Id
            )
            SELECT
                COUNT(*) AS TotalCount,
                COALESCE(SUM(CASE WHEN Balance > 0 AND Balance <= CASE WHEN MinStock > 0 THEN MinStock ELSE @Fallback END THEN 1 ELSE 0 END), 0) AS LowStockCount,
                COALESCE(SUM(CASE WHEN Balance <= 0 THEN 1 ELSE 0 END), 0) AS DepletedCount
            FROM FilteredBalances
            """;

        return await conn.QuerySingleAsync<(int TotalCount, int LowStockCount, int DepletedCount)>(
            new CommandDefinition(sql, @params, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private void ValidateCard(StockCard s, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) throw new InvalidOperationException(LocalizationManager.L("name_required"));
        if (string.IsNullOrWhiteSpace(s.Code)) throw new InvalidOperationException(LocalizationManager.L("code_required"));

        s.Name = s.Name.Trim();
        s.Code = s.Code.Trim();
        s.CardType = s.CardTypeEnum.ToDbString();
        if (s.MinStock < 0) s.MinStock = 0;

        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM StockCards WHERE Code=@Code COLLATE NOCASE AND Id<>@Id",
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

            if (parentType == null || parentType.ToCardType() != CardType.Parent)
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
        else if (s.Id > 0)
        {
            // Child card cannot still have children pointing to it
            var hasChildren = conn.ExecuteScalar<bool>(
                "SELECT EXISTS(SELECT 1 FROM StockCards WHERE ParentId=@Id)",
                new { Id = s.Id },
                transaction: trans);
            if (hasChildren)
                throw new InvalidOperationException(LocalizationManager.L("parent_card_has_children"));
        }
    }
}
