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

public sealed class MovementRepository : RepositoryBase, IMovementRepository
{
    public MovementRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public List<StockMovement> GetAll(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType, string? category)
    {
        var list = new List<StockMovement>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(stockCardId, startDate, endDate, department, movementType, category);
        BindQueryParams(cmd, stockCardId, startDate, endDate, department, movementType, category);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(Read(reader));
        return list;
    }

    public async Task<List<StockMovement>> GetAllAsync(int? stockCardId, DateTime? startDate, DateTime? endDate, string? department, string? movementType, string? category, CancellationToken cancellationToken = default)
    {
        var list = new List<StockMovement>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildQuery(stockCardId, startDate, endDate, department, movementType, category);
        BindQueryParams(cmd, stockCardId, startDate, endDate, department, movementType, category);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) list.Add(Read(reader));
        return list;
    }

    public void Add(StockMovement movement)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateMovement(movement, conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO StockMovements (StockCardId, Type, Quantity, Recipient, Department, Date, Description)
            VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
        BindMovementParams(cmd, movement);
        cmd.ExecuteNonQuery();
        movement.Id = GetLastId(conn);
        LogAudit("Insert", "StockMovements", movement.Id, $"{movement.Type}: {movement.Quantity}");
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateMovement(movement, conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO StockMovements (StockCardId, Type, Quantity, Recipient, Department, Date, Description)
            VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
        BindMovementParams(cmd, movement);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        movement.Id = GetLastId(conn);
    }

    public void AddBulk(IEnumerable<StockMovement> movements)
    {
        var list = movements.ToList();
        if (list.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            var cardIds = list.Select(m => m.StockCardId).Distinct().ToList();
            var balances = GetBalances(conn, trans, cardIds);

            foreach(var m in list) {
                if (!balances.TryGetValue(m.StockCardId, out var info))
                    throw new InvalidOperationException($"Stock card not found: ID {m.StockCardId}");

                if (info.Type == "Parent")
                    throw new InvalidOperationException($"'{info.Name}' is a parent card, cannot add movements.");

                if (m.TypeEnum == MovementType.Exit && info.Balance < m.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for '{info.Name}'. Available: {info.Balance}, Requested: {m.Quantity}");

                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "INSERT INTO StockMovements (StockCardId, Type, Quantity, Recipient, Department, Date, Description) VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
                BindMovementParams(cmd, m);
                cmd.ExecuteNonQuery();

                info.Balance += (m.TypeEnum == MovementType.Entry ? m.Quantity : -m.Quantity);
            }
            trans.Commit();
            LogAudit("Insert", "StockMovements", 0, "Bulk Insert");
        } catch { trans.Rollback(); throw; }
    }

    public async Task AddBulkAsync(IEnumerable<StockMovement> movements, CancellationToken cancellationToken = default)
    {
        var list = movements.ToList();
        if (list.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try {
            var cardIds = list.Select(m => m.StockCardId).Distinct().ToList();
            var balances = GetBalances(conn, (SqliteTransaction)trans, cardIds);

            foreach(var m in list) {
                if (!balances.TryGetValue(m.StockCardId, out var info))
                    throw new InvalidOperationException($"Stock card not found: ID {m.StockCardId}");

                if (info.Type == "Parent")
                    throw new InvalidOperationException($"'{info.Name}' is a parent card, cannot add movements.");

                if (m.TypeEnum == MovementType.Exit && info.Balance < m.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for '{info.Name}'. Available: {info.Balance}, Requested: {m.Quantity}");

                using var cmd = conn.CreateCommand();
                cmd.Transaction = (SqliteTransaction)trans;
                cmd.CommandText = "INSERT INTO StockMovements (StockCardId, Type, Quantity, Recipient, Department, Date, Description) VALUES ($sk, $tr, $mk, $kv, $dp, $th, $ac)";
                BindMovementParams(cmd, m);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                info.Balance += (m.TypeEnum == MovementType.Entry ? m.Quantity : -m.Quantity);
            }
            await trans.CommitAsync(cancellationToken);
            LogAudit("Insert", "StockMovements", 0, "Bulk Insert (Async)");
        } catch { await trans.RollbackAsync(cancellationToken); throw; }
    }

    private class CardBalanceInfo { public string Name = ""; public string Type = ""; public double Balance; }

    private Dictionary<int, CardBalanceInfo> GetBalances(SqliteConnection conn, SqliteTransaction trans, List<int> ids)
    {
        var result = new Dictionary<int, CardBalanceInfo>();
        if (ids.Count == 0) return result;

        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;

        var paramNames = ids.Select((_, i) => $"$id{i}").ToList();
        cmd.CommandText = $@"
            SELECT s.Id, s.Name, s.CardType,
            COALESCE((SELECT SUM(CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements WHERE StockCardId=s.Id), 0)
            FROM StockCards s WHERE s.Id IN ({string.Join(",", paramNames)})";

        for (int i = 0; i < ids.Count; i++)
            cmd.Parameters.AddWithValue(paramNames[i], ids[i]);

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

    public void Update(StockMovement movement)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateUpdate(movement, conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE StockMovements SET 
                StockCardId=$sk, Type=$tr, Quantity=$mk, Recipient=$kv, 
                Department=$dp, Date=$th, Description=$ac 
            WHERE Id=$id";
        BindMovementParams(cmd, movement);
        cmd.Parameters.AddWithValue("$id", movement.Id);
        cmd.ExecuteNonQuery();
        LogAudit("Update", "StockMovements", movement.Id, $"{movement.Type}: {movement.Quantity}");
    }

    public void Delete(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        ValidateDeletion(new[] { id }, conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM StockMovements WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        LogAudit("Delete", "StockMovements", id, "");
    }

    public void DeleteBulk(IEnumerable<int> ids)
    {
        var list = ids.ToList();
        if (list.Count == 0) throw new InvalidOperationException("List cannot be empty.");

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            var paramNames = list.Select((_, i) => $"$did{i}").ToList();
            string paramList = string.Join(",", paramNames);

            using (var cmdExist = conn.CreateCommand()) {
                cmdExist.Transaction = trans;
                cmdExist.CommandText = $"SELECT COUNT(*) FROM StockMovements WHERE Id IN ({paramList})";
                for (int i = 0; i < list.Count; i++) cmdExist.Parameters.AddWithValue(paramNames[i], list[i]);
                var count = Convert.ToInt32(cmdExist.ExecuteScalar());
                if (count != list.Count) throw new InvalidOperationException("Some movements were not found.");
            }

            ValidateDeletion(list, conn, trans);

            foreach(var id in list) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "DELETE FROM StockMovements WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

    public void UpdateBulk(IEnumerable<StockMovement> movements)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            foreach(var m in movements) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = "UPDATE StockMovements SET StockCardId=$sk, Type=$tr, Quantity=$mk, Recipient=$kv, Department=$dp, Date=$th, Description=$ac WHERE Id=$id";
                BindMovementParams(cmd, m);
                cmd.Parameters.AddWithValue("$id", m.Id);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
        } catch { trans.Rollback(); throw; }
    }

    public async Task<List<(DateTime Date, double Entry, double Exit)>> GetLast7DaysSummaryAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<(DateTime, double, double)>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT DATE(Date) as Day,
            SUM(CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE 0 END) as Entry,
            SUM(CASE WHEN Type IN ('Exit', 'Cikis', 'Çıkış') THEN Quantity ELSE 0 END) as Exit
            FROM StockMovements
            WHERE Date >= $bas
            GROUP BY DATE(Date)
            ORDER BY Day";
        cmd.Parameters.AddWithValue("$bas", DateTime.Today.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var data = new Dictionary<DateTime, (double e, double x)>();
        while(await reader.ReadAsync(cancellationToken)) {
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
        cmd.CommandText = "SELECT DISTINCT Recipient FROM StockMovements WHERE Recipient IS NOT NULL AND Recipient <> '' ORDER BY Recipient";
        using var reader = cmd.ExecuteReader();
        while(reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    private string BuildQuery(int? cardId, DateTime? start, DateTime? end, string? dept, string? type, string? cat, string? search = null)
    {
        var sql = "SELECT h.*, s.Name as StockCardName, s.Code as StockCardCode FROM StockMovements h JOIN StockCards s ON h.StockCardId = s.Id WHERE 1=1";
        if(cardId.HasValue) sql += " AND h.StockCardId=$sk";
        if(start.HasValue) sql += " AND h.Date >= $ts";
        if(end.HasValue) sql += " AND h.Date < $te";
        if(!string.IsNullOrEmpty(dept)) sql += " AND h.Department=$dp";
        if(!string.IsNullOrEmpty(type)) sql += " AND h.Type=$tr";
        if(!string.IsNullOrEmpty(cat)) sql += " AND s.Category=$ct";
        if(!string.IsNullOrEmpty(search)) sql += " AND (s.Name LIKE $q OR s.Code LIKE $q OR h.Recipient LIKE $q OR h.Department LIKE $q)";
        sql += " ORDER BY h.Date DESC, h.Id DESC";
        return sql;
    }

    private void BindQueryParams(SqliteCommand cmd, int? cardId, DateTime? start, DateTime? end, string? dept, string? type, string? cat, string? search = null)
    {
        if(cardId.HasValue) cmd.Parameters.AddWithValue("$sk", cardId.Value);
        if(start.HasValue) cmd.Parameters.AddWithValue("$ts", start.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if(end.HasValue) cmd.Parameters.AddWithValue("$te", end.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if(!string.IsNullOrEmpty(dept)) cmd.Parameters.AddWithValue("$dp", dept);
        if(!string.IsNullOrEmpty(type)) cmd.Parameters.AddWithValue("$tr", type);
        if(!string.IsNullOrEmpty(cat)) cmd.Parameters.AddWithValue("$ct", cat);
        if(!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("$q", $"%{search}%");
    }

    private void BindMovementParams(SqliteCommand cmd, StockMovement m)
    {
        cmd.Parameters.AddWithValue("$sk", m.StockCardId);
        cmd.Parameters.AddWithValue("$tr", m.Type);
        cmd.Parameters.AddWithValue("$mk", m.Quantity);
        cmd.Parameters.AddWithValue("$kv", m.Recipient ?? "");
        cmd.Parameters.AddWithValue("$dp", m.Department ?? "");
        cmd.Parameters.AddWithValue("$th", m.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$ac", m.Description ?? "");
    }

    private StockMovement Read(SqliteDataReader reader)
    {
        DateTime dateVal = DateTime.Now;
        if (!reader.IsDBNull(reader.GetOrdinal("Date")))
        {
            string dateStr = reader.GetString(reader.GetOrdinal("Date"));
            if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateVal))
            {
                DateTime.TryParse(dateStr, out dateVal);
            }
        }

        return new StockMovement {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            StockCardId = reader.GetInt32(reader.GetOrdinal("StockCardId")),
            Type = reader.GetString(reader.GetOrdinal("Type")),
            Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
            Recipient = reader.IsDBNull(reader.GetOrdinal("Recipient")) ? "" : reader.GetString(reader.GetOrdinal("Recipient")),
            Department = reader.IsDBNull(reader.GetOrdinal("Department")) ? "" : reader.GetString(reader.GetOrdinal("Department")),
            Date = dateVal,
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
            StockCardName = reader.GetString(reader.GetOrdinal("StockCardName")),
            StockCardCode = reader.GetString(reader.GetOrdinal("StockCardCode"))
        };
    }

    private int GetLastId(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(cmd.ExecuteScalar());
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

        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        cmd.CommandText = @"
            SELECT CardType,
            (SELECT COALESCE(SUM(CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END), 0) FROM StockMovements WHERE StockCardId=s.Id) as CurrentStock
            FROM StockCards s WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", m.StockCardId);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            if (reader.GetString(0) == "Parent") throw new InvalidOperationException("Cannot add movements to parent cards.");
            if (m.TypeEnum == MovementType.Exit)
            {
                double current = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                if (current < m.Quantity) throw new InvalidOperationException("Insufficient stock.");
            }
        }
        else throw new InvalidOperationException("Stock card not found.");
    }

    private void ValidateDeletion(IEnumerable<int> ids, SqliteConnection conn, SqliteTransaction? trans = null)
    {
        var idList = ids.ToList();
        var paramNames = idList.Select((_, i) => $"$vid{i}").ToList();
        string paramList = string.Join(",", paramNames);

        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        cmd.CommandText = $@"
            SELECT StockCardId FROM StockMovements WHERE Id IN ({paramList}) GROUP BY StockCardId";
        for (int i = 0; i < idList.Count; i++) cmd.Parameters.AddWithValue(paramNames[i], idList[i]);

        var cardIds = new List<int>();
        using (var reader = cmd.ExecuteReader())
            while (reader.Read()) cardIds.Add(reader.GetInt32(0));

        foreach (var cardId in cardIds)
        {
            using var cmdCheck = conn.CreateCommand();
            if (trans != null) cmdCheck.Transaction = trans;

            var checkParamNames = idList.Select((_, i) => $"$cid{i}").ToList();
            cmdCheck.CommandText = $@"
                SELECT SUM(CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END)
                FROM StockMovements 
                WHERE StockCardId=$cardId AND Id NOT IN ({string.Join(",", checkParamNames)})";
            cmdCheck.Parameters.AddWithValue("$cardId", cardId);
            for (int i = 0; i < idList.Count; i++) cmdCheck.Parameters.AddWithValue(checkParamNames[i], idList[i]);

            var result = cmdCheck.ExecuteScalar();
            double balance = (result == null || result == DBNull.Value) ? 0 : Convert.ToDouble(result);
            if (balance < 0) throw new InvalidOperationException("Deletion invalid: would result in negative stock.");
        }
    }

    public async Task<List<StockMovement>> GetPagedAsync(int page, int pageSize, int? stockCardId = null, DateTime? startDate = null, DateTime? endDate = null, string? department = null, string? movementType = null, string? category = null, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var list = new List<StockMovement>();
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        string baseQuery = BuildQuery(stockCardId, startDate, endDate, department, movementType, category, searchTerm);
        cmd.CommandText = $"{baseQuery} LIMIT $limit OFFSET $offset";
        BindQueryParams(cmd, stockCardId, startDate, endDate, department, movementType, category, searchTerm);
        cmd.Parameters.AddWithValue("$limit", pageSize);
        cmd.Parameters.AddWithValue("$offset", (page - 1) * pageSize);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) list.Add(Read(reader));
        return list;
    }

    public async Task<int> GetCountAsync(int? stockCardId = null, DateTime? startDate = null, DateTime? endDate = null, string? department = null, string? movementType = null, string? category = null, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        string sql = "SELECT COUNT(*) FROM StockMovements h JOIN StockCards s ON h.StockCardId = s.Id WHERE 1=1";
        if(stockCardId.HasValue) sql += " AND h.StockCardId=$sk";
        if(startDate.HasValue) sql += " AND h.Date >= $ts";
        if(endDate.HasValue) sql += " AND h.Date < $te";
        if(!string.IsNullOrEmpty(department)) sql += " AND h.Department=$dp";
        if(!string.IsNullOrEmpty(movementType)) sql += " AND h.Type=$tr";
        if(!string.IsNullOrEmpty(category)) sql += " AND s.Category=$ct";
        if(!string.IsNullOrEmpty(searchTerm)) sql += " AND (s.Name LIKE $q OR s.Code LIKE $q OR h.Recipient LIKE $q OR h.Department LIKE $q)";
        
        cmd.CommandText = sql;
        BindQueryParams(cmd, stockCardId, startDate, endDate, department, movementType, category, searchTerm);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private void ValidateUpdate(StockMovement m, SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT SUM(CASE WHEN Id=$mid THEN 0 ELSE (CASE WHEN Type IN ('Entry', 'Giris', 'Giriş') THEN Quantity ELSE -Quantity END) END)
            FROM StockMovements WHERE StockCardId=$cid";
        cmd.Parameters.AddWithValue("$mid", m.Id);
        cmd.Parameters.AddWithValue("$cid", m.StockCardId);
        
        var baseResult = cmd.ExecuteScalar();
        double baseBalance = (baseResult == null || baseResult == DBNull.Value) ? 0 : Convert.ToDouble(baseResult);
        double newImpact = (m.TypeEnum == MovementType.Entry) ? m.Quantity : (m.TypeEnum == MovementType.Exit ? -m.Quantity : 0);
        
        if (baseBalance + newImpact < 0) throw new InvalidOperationException("Update invalid: would result in negative stock.");
    }
}
