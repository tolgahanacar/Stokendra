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

public sealed class MovementRepository(IDbConnectionFactory connectionFactory) 
    : RepositoryBase(connectionFactory), IMovementRepository
{
    public List<StockMovement> GetAll(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType, string? category, string? recipient)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        string sql = BuildQuery("SELECT h.*, s.Name as StockCardName, s.Code as StockCardCode FROM StockMovements h JOIN StockCards s ON h.StockCardId = s.Id", stockCardId, startDate, endDate, department, movementType, category, null, recipient, @params);
        sql += " ORDER BY h.Date DESC, h.Id DESC";
        return conn.Query<StockMovement>(sql, @params).ToList();
    }

    public async Task<List<StockMovement>> GetAllAsync(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType, string? category, string? recipient, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        string sql = BuildQuery("SELECT h.*, s.Name as StockCardName, s.Code as StockCardCode FROM StockMovements h JOIN StockCards s ON h.StockCardId = s.Id", stockCardId, startDate, endDate, department, movementType, category, null, recipient, @params);
        sql += " ORDER BY h.Date DESC, h.Id DESC";
        var result = await conn.QueryAsync<StockMovement>(new CommandDefinition(sql, @params, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public void Add(StockMovement movement)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateMovement(movement, conn);
        var sql = """
            INSERT INTO StockMovements (StockCardId, Type, Quantity, Recipient, Department, Date, Description)
            VALUES (@StockCardId, @Type, @Quantity, @Recipient, @Department, @DateString, @Description);
            SELECT last_insert_rowid();
            """;
        movement.Id = conn.ExecuteScalar<int>(sql, new {
            movement.StockCardId,
            movement.Type,
            movement.Quantity,
            Recipient = movement.Recipient ?? "",
            Department = movement.Department ?? "",
            DateString = movement.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Description = movement.Description ?? ""
        });
        LogAudit("Insert", "StockMovements", movement.Id, $"{movement.Type}: {movement.Quantity}");
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateMovement(movement, conn);
        var sql = """
            INSERT INTO StockMovements (StockCardId, Type, Quantity, Recipient, Department, Date, Description)
            VALUES (@StockCardId, @Type, @Quantity, @Recipient, @Department, @DateString, @Description);
            SELECT last_insert_rowid();
            """;
        movement.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new {
            movement.StockCardId,
            movement.Type,
            movement.Quantity,
            Recipient = movement.Recipient ?? "",
            Department = movement.Department ?? "",
            DateString = movement.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Description = movement.Description ?? ""
        }, cancellationToken: cancellationToken));
        LogAudit("Insert", "StockMovements", movement.Id, $"{movement.Type}: {movement.Quantity}");
    }

    public void AddBulk(IEnumerable<StockMovement> movements)
    {
        var list = movements.ToList();
        if (list.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try 
        {
            var cardIds = list.Select(m => m.StockCardId).Distinct().ToList();
            var balances = GetBalances(conn, trans, cardIds);

            foreach(var m in list) 
            {
                if (!balances.TryGetValue(m.StockCardId, out var info))
                    throw new InvalidOperationException($"Stock card not found: ID {m.StockCardId}");

                if (info.Type == "Parent")
                    throw new InvalidOperationException($"'{info.Name}' is a parent card, cannot add movements.");

                if (m.TypeEnum == MovementType.Exit && info.Balance < m.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for '{info.Name}'. Available: {info.Balance}, Requested: {m.Quantity}");

                conn.Execute("""
                    INSERT INTO StockMovements (StockCardId, Type, Quantity, Recipient, Department, Date, Description) 
                    VALUES (@StockCardId, @Type, @Quantity, @Recipient, @Department, @DateString, @Description)
                    """, 
                    new {
                        m.StockCardId,
                        m.Type,
                        m.Quantity,
                        Recipient = m.Recipient ?? "",
                        Department = m.Department ?? "",
                        DateString = m.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        Description = m.Description ?? ""
                    }, 
                    transaction: trans);

                info.Balance += (m.TypeEnum == MovementType.Entry ? m.Quantity : -m.Quantity);
            }
            trans.Commit();
            LogAudit("Insert", "StockMovements", 0, "Bulk Insert");
        } 
        catch 
        { 
            trans.Rollback(); 
            throw; 
        }
    }

    public async Task AddBulkAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default)
    {
        var list = movements.ToList();
        if (list.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try 
        {
            var cardIds = list.Select(m => m.StockCardId).Distinct().ToList();
            var balances = GetBalances(conn, (SqliteTransaction)trans, cardIds);

            foreach(var m in list) 
            {
                if (!balances.TryGetValue(m.StockCardId, out var info))
                    throw new InvalidOperationException($"Stock card not found: ID {m.StockCardId}");

                if (info.Type == "Parent")
                    throw new InvalidOperationException($"'{info.Name}' is a parent card, cannot add movements.");

                if (m.TypeEnum == MovementType.Exit && info.Balance < m.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for '{info.Name}'. Available: {info.Balance}, Requested: {m.Quantity}");

                await conn.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO StockMovements (StockCardId, Type, Quantity, Recipient, Department, Date, Description) 
                    VALUES (@StockCardId, @Type, @Quantity, @Recipient, @Department, @DateString, @Description)
                    """, 
                    new {
                        m.StockCardId,
                        m.Type,
                        m.Quantity,
                        Recipient = m.Recipient ?? "",
                        Department = m.Department ?? "",
                        DateString = m.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        Description = m.Description ?? ""
                    }, 
                    transaction: trans, 
                    cancellationToken: cancellationToken));

                info.Balance += (m.TypeEnum == MovementType.Entry ? m.Quantity : -m.Quantity);
            }
            await trans.CommitAsync(cancellationToken);
            LogAudit("Insert", "StockMovements", 0, "Bulk Insert (Async)");
        } 
        catch 
        { 
            await trans.RollbackAsync(cancellationToken); 
            throw; 
        }
    }

    private class CardBalanceInfo { public string Name = ""; public string Type = ""; public double Balance; }

    private Dictionary<int, CardBalanceInfo> GetBalances(SqliteConnection conn, SqliteTransaction trans, List<int> ids)
    {
        if (ids.Count == 0) return new Dictionary<int, CardBalanceInfo>();

        var sql = """
            SELECT s.Id, s.Name, s.CardType as Type,
            COALESCE((SELECT SUM(CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements WHERE StockCardId=s.Id), 0) as Balance
            FROM StockCards s WHERE s.Id IN @Ids
            """;

        var result = conn.Query<(int Id, string Name, string Type, double Balance)>(sql, new { Ids = ids }, transaction: trans);
        return result.ToDictionary(r => r.Id, r => new CardBalanceInfo { Name = r.Name, Type = r.Type, Balance = r.Balance });
    }

    public void Update(StockMovement movement)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateUpdate(movement, conn);
        var sql = """
            UPDATE StockMovements SET 
                StockCardId=@StockCardId, Type=@Type, Quantity=@Quantity, Recipient=@Recipient, 
                Department=@Department, Date=@DateString, Description=@Description 
            WHERE Id=@Id
            """;
        conn.Execute(sql, new {
            movement.StockCardId,
            movement.Type,
            movement.Quantity,
            Recipient = movement.Recipient ?? "",
            Department = movement.Department ?? "",
            DateString = movement.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Description = movement.Description ?? "",
            movement.Id
        });
        LogAudit("Update", "StockMovements", movement.Id, $"{movement.Type}: {movement.Quantity}");
    }

    public async Task UpdateAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateUpdate(movement, conn);
        var sql = """
            UPDATE StockMovements SET 
                StockCardId=@StockCardId, Type=@Type, Quantity=@Quantity, Recipient=@Recipient, 
                Department=@Department, Date=@DateString, Description=@Description 
            WHERE Id=@Id
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, new {
            movement.StockCardId,
            movement.Type,
            movement.Quantity,
            Recipient = movement.Recipient ?? "",
            Department = movement.Department ?? "",
            DateString = movement.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Description = movement.Description ?? "",
            movement.Id
        }, cancellationToken: cancellationToken));
        LogAudit("Update", "StockMovements", movement.Id, $"{movement.Type}: {movement.Quantity}");
    }

    public void Delete(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateDeletion([id], conn);
        conn.Execute("DELETE FROM StockMovements WHERE Id=@Id", new { Id = id });
        LogAudit("Delete", "StockMovements", id, "");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateDeletion([id], conn);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM StockMovements WHERE Id=@Id", new { Id = id }, cancellationToken: cancellationToken));
        LogAudit("Delete", "StockMovements", id, "");
    }

    public void DeleteBulk(IEnumerable<int> ids)
    {
        var list = ids.ToList();
        if (list.Count == 0) throw new InvalidOperationException("List cannot be empty.");

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try 
        {
            var count = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM StockMovements WHERE Id IN @Ids", 
                new { Ids = list }, 
                transaction: trans);
            if (count != list.Count) throw new InvalidOperationException("Some movements were not found.");

            ValidateDeletion(list, conn, trans);

            conn.Execute("DELETE FROM StockMovements WHERE Id IN @Ids", new { Ids = list }, transaction: trans);
            trans.Commit();
        } 
        catch 
        { 
            trans.Rollback(); 
            throw; 
        }
    }

    public async Task DeleteBulkAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var list = ids.ToList();
        if (list.Count == 0) throw new InvalidOperationException("List cannot be empty.");

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try 
        {
            var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM StockMovements WHERE Id IN @Ids", 
                new { Ids = list }, 
                transaction: trans,
                cancellationToken: cancellationToken));
            if (count != list.Count) throw new InvalidOperationException("Some movements were not found.");

            ValidateDeletion(list, conn, (SqliteTransaction)trans);

            await conn.ExecuteAsync(new CommandDefinition("DELETE FROM StockMovements WHERE Id IN @Ids", new { Ids = list }, transaction: trans, cancellationToken: cancellationToken));
            await trans.CommitAsync(cancellationToken);
        } 
        catch 
        { 
            await trans.RollbackAsync(cancellationToken); 
            throw; 
        }
    }

    public void UpdateBulk(IEnumerable<StockMovement> movements)
    {
        var list = movements.ToList();
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try 
        {
            ValidateBulkUpdate(list, conn, trans);

            var sql = """
                UPDATE StockMovements SET 
                    StockCardId=@StockCardId, Type=@Type, Quantity=@Quantity, Recipient=@Recipient, 
                    Department=@Department, Date=@DateString, Description=@Description 
                WHERE Id=@Id
                """;
            var data = list.Select(m => new {
                m.StockCardId,
                m.Type,
                m.Quantity,
                Recipient = m.Recipient ?? "",
                Department = m.Department ?? "",
                DateString = m.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Description = m.Description ?? "",
                m.Id
            });
            conn.Execute(sql, data, transaction: trans);
            trans.Commit();
            LogAudit("Update", "StockMovements", 0, "Bulk Update");
        } 
        catch 
        { 
            trans.Rollback(); 
            throw; 
        }
    }

    public async Task UpdateBulkAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default)
    {
        var list = movements.ToList();
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try 
        {
            ValidateBulkUpdate(list, conn, (SqliteTransaction)trans);

            var sql = """
                UPDATE StockMovements SET 
                    StockCardId=@StockCardId, Type=@Type, Quantity=@Quantity, Recipient=@Recipient, 
                    Department=@Department, Date=@DateString, Description=@Description 
                WHERE Id=@Id
                """;
            var data = list.Select(m => new {
                m.StockCardId,
                m.Type,
                m.Quantity,
                Recipient = m.Recipient ?? "",
                Department = m.Department ?? "",
                DateString = m.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Description = m.Description ?? "",
                m.Id
            });
            await conn.ExecuteAsync(new CommandDefinition(sql, data, transaction: trans, cancellationToken: cancellationToken));
            await trans.CommitAsync(cancellationToken);
            LogAudit("Update", "StockMovements", 0, "Bulk Update (Async)");
        } 
        catch 
        { 
            await trans.RollbackAsync(cancellationToken); 
            throw; 
        }
    }

    private void ValidateBulkUpdate(IEnumerable<StockMovement> movements, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        var list = movements.ToList();
        if (list.Count == 0) return;

        foreach (var m in list)
        {
            if (double.IsNaN(m.Quantity) || double.IsInfinity(m.Quantity) || m.Quantity > 1_000_000_000) 
                throw new InvalidOperationException("Invalid quantity value.");
            
            if (m.Type != "Entry" && m.Type != "Exit" && m.Type != "Blank" && m.Type != "Giris" && m.Type != "Cikis" && m.Type != "Bos") 
                throw new InvalidOperationException("Invalid movement type.");
            
            if (m.Quantity < 0) throw new InvalidOperationException("Quantity cannot be negative.");
            if (m.Quantity == 0 && m.TypeEnum != MovementType.Blank) throw new InvalidOperationException("Quantity cannot be zero.");
            
            if (string.IsNullOrWhiteSpace(m.Type)) throw new InvalidOperationException("Type field cannot be empty.");
            if (m.StockCardId <= 0) throw new InvalidOperationException("Invalid stock card ID.");

            var cardType = conn.ExecuteScalar<string>(
                "SELECT CardType FROM StockCards WHERE Id=@Id", 
                new { Id = m.StockCardId }, 
                transaction: trans);
            if (cardType == "Parent")
                throw new InvalidOperationException("Cannot associate movements with parent cards.");
        }

        var movementIds = list.Select(m => m.Id).ToList();
        var cards = list.GroupBy(m => m.StockCardId);

        foreach (var cardGroup in cards)
        {
            int cardId = cardGroup.Key;
            
            var baseResult = conn.ExecuteScalar<double?>(
                "SELECT SUM(CASE WHEN Id IN @Ids THEN 0 ELSE (CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END) END) FROM StockMovements WHERE StockCardId=@StockCardId",
                new { Ids = movementIds, StockCardId = cardId },
                transaction: trans);

            double baseBalance = baseResult ?? 0;
            double newImpact = cardGroup.Sum(m => (m.TypeEnum == MovementType.Entry) ? m.Quantity : (m.TypeEnum == MovementType.Exit ? -m.Quantity : 0));
            
            if (baseBalance + newImpact < 0)
            {
                var cardName = conn.ExecuteScalar<string>(
                    "SELECT Name FROM StockCards WHERE Id=@Id", 
                    new { Id = cardId }, 
                    transaction: trans) ?? $"ID {cardId}";
                throw new InvalidOperationException($"Update invalid: would result in negative stock for '{cardName}'.");
            }
        }
    }

    public async Task<List<(DateTime Date, double Entry, double Exit)>> GetLast7DaysSummaryAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<(DateTime, double, double)>();
        using var conn = ConnectionFactory.CreateConnection();
        var sql = """
            SELECT DATE(Date) as Day,
            SUM(CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE 0 END) as Entry,
            SUM(CASE WHEN Type IN ('Exit', 'Cikis', 'Çıkış') THEN Quantity ELSE 0 END) as Exit
            FROM StockMovements
            WHERE Date >= @StartDate
            GROUP BY DATE(Date)
            ORDER BY Day
            """;
        var startDateString = DateTime.Today.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        
        var queryResult = await conn.QueryAsync<(string Day, double Entry, double Exit)>(new CommandDefinition(sql, new { StartDate = startDateString }, cancellationToken: cancellationToken));
        var data = queryResult.ToDictionary(r => DateTime.Parse(r.Day), r => (r.Entry, r.Exit));
        
        for(int i=0; i<7; i++) 
        {
            var day = DateTime.Today.AddDays(-6+i);
            if(data.TryGetValue(day, out var vals)) list.Add((day, vals.Entry, vals.Exit));
            else list.Add((day, 0, 0));
        }
        return list;
    }

    public List<string> GetDeliveredPersons()
    {
        using var conn = ConnectionFactory.CreateConnection();
        return conn.Query<string>("SELECT DISTINCT Recipient FROM StockMovements WHERE Recipient IS NOT NULL AND Recipient <> '' ORDER BY Recipient").ToList();
    }

    private string BuildQuery(string baseSelect, int? cardId, DateTime? start, DateTime? end, string? dept, string? type, string? cat, string? search, string? recipient, DynamicParameters @params)
    {
        var sql = baseSelect + " WHERE 1=1";
        if(cardId.HasValue) 
        {
            sql += " AND h.StockCardId=@CardId";
            @params.Add("CardId", cardId.Value);
        }
        if(start.HasValue) 
        {
            sql += " AND h.Date >= @Start";
            @params.Add("Start", start.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
        if(end.HasValue) 
        {
            sql += " AND h.Date < @End";
            @params.Add("End", end.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
        if(!string.IsNullOrEmpty(dept)) 
        {
            sql += " AND h.Department=@Dept";
            @params.Add("Dept", dept);
        }
        if(!string.IsNullOrEmpty(type)) 
        {
            sql += " AND h.Type=@Type";
            @params.Add("Type", type);
        }
        if(!string.IsNullOrEmpty(cat)) 
        {
            sql += " AND s.Category=@Cat";
            @params.Add("Cat", cat);
        }
        if(!string.IsNullOrEmpty(recipient)) 
        {
            sql += " AND h.Recipient=@Recipient";
            @params.Add("Recipient", recipient);
        }
        if(!string.IsNullOrEmpty(search)) 
        {
            sql += " AND (s.Name LIKE @Search OR s.Code LIKE @Search OR h.Recipient LIKE @Search OR h.Department LIKE @Search OR h.Description LIKE @Search)";
            @params.Add("Search", $"%{search}%");
        }
        return sql;
    }

    private void ValidateMovement(StockMovement m, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        if (double.IsNaN(m.Quantity) || double.IsInfinity(m.Quantity) || m.Quantity > 1_000_000_000) 
            throw new InvalidOperationException("Invalid quantity value.");
        
        if (m.Type != "Entry" && m.Type != "Exit" && m.Type != "Blank" && m.Type != "Giris" && m.Type != "Cikis" && m.Type != "Bos") 
            throw new InvalidOperationException("Invalid movement type.");
        
        if (m.Quantity < 0) throw new InvalidOperationException("Quantity cannot be negative.");
        if (m.Quantity == 0 && m.TypeEnum != MovementType.Blank) throw new InvalidOperationException("Quantity cannot be zero.");
        
        if (string.IsNullOrWhiteSpace(m.Type)) throw new InvalidOperationException("Type field cannot be empty.");
        if (m.StockCardId <= 0) throw new InvalidOperationException("Invalid stock card ID.");

        var row = conn.QueryFirstOrDefault(
            "SELECT CardType, (SELECT COALESCE(SUM(CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END), 0) FROM StockMovements WHERE StockCardId=s.Id) as CurrentStock FROM StockCards s WHERE Id=@Id",
            new { Id = m.StockCardId },
            transaction: trans);

        if (row != null)
        {
            if (row.CardType == "Parent") throw new InvalidOperationException("Cannot add movements to parent cards.");
            if (m.TypeEnum == MovementType.Exit)
            {
                double current = (double)row.CurrentStock;
                if (current < m.Quantity) throw new InvalidOperationException("Insufficient stock.");
            }
        }
        else throw new InvalidOperationException("Stock card not found.");
    }

    private void ValidateDeletion(IEnumerable<int> ids, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        var idList = ids.ToList();
        
        var cardIds = conn.Query<int>(
            "SELECT StockCardId FROM StockMovements WHERE Id IN @Ids GROUP BY StockCardId",
            new { Ids = idList },
            transaction: trans).ToList();

        foreach (var cardId in cardIds)
        {
            var result = conn.ExecuteScalar<double?>(
                "SELECT SUM(CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements WHERE StockCardId=@CardId AND Id NOT IN @Ids",
                new { CardId = cardId, Ids = idList },
                transaction: trans);

            double balance = result ?? 0;
            if (balance < 0) throw new InvalidOperationException("Deletion invalid: would result in negative stock.");
        }
    }

    public async Task<List<StockMovement>> GetPagedAsync(int page, int pageSize, int? stockCardId = null, DateTime? startDate = null, DateTime? endDate = null, string? department = null, string? movementType = null, string? category = null, string? searchTerm = null, string? recipient = null, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        string baseQuery = BuildQuery("SELECT h.*, s.Name as StockCardName, s.Code as StockCardCode FROM StockMovements h JOIN StockCards s ON h.StockCardId = s.Id", stockCardId, startDate, endDate, department, movementType, category, searchTerm, recipient, @params);
        string sql = $"{baseQuery} ORDER BY h.Date DESC, h.Id DESC LIMIT @Limit OFFSET @Offset";
        @params.Add("Limit", pageSize);
        @params.Add("Offset", (page - 1) * pageSize);

        var result = await conn.QueryAsync<StockMovement>(new CommandDefinition(sql, @params, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<int> GetCountAsync(int? stockCardId = null, DateTime? startDate = null, DateTime? endDate = null, string? department = null, string? movementType = null, string? category = null, string? searchTerm = null, string? recipient = null, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var @params = new DynamicParameters();
        string sql = BuildQuery("SELECT COUNT(*) FROM StockMovements h JOIN StockCards s ON h.StockCardId = s.Id", stockCardId, startDate, endDate, department, movementType, category, searchTerm, recipient, @params);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, @params, cancellationToken: cancellationToken));
    }

    private void ValidateUpdate(StockMovement m, SqliteConnection conn)
    {
        var baseResult = conn.ExecuteScalar<double?>(
            "SELECT SUM(CASE WHEN Id=@Id THEN 0 ELSE (CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END) END) FROM StockMovements WHERE StockCardId=@StockCardId",
            new { Id = m.Id, StockCardId = m.StockCardId });
            
        double baseBalance = baseResult ?? 0;
        double newImpact = (m.TypeEnum == MovementType.Entry) ? m.Quantity : (m.TypeEnum == MovementType.Exit ? -m.Quantity : 0);
        
        if (baseBalance + newImpact < 0) throw new InvalidOperationException("Update invalid: would result in negative stock.");
    }
}
