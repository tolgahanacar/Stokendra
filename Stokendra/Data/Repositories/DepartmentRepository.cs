using Dapper;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Repositories;

public sealed class DepartmentRepository(IDbConnectionFactory connectionFactory)
    : RepositoryBase(connectionFactory), IDepartmentRepository
{
    public async Task<List<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var result = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT Name FROM Departments ORDER BY Name",
            cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task AddAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        string cleanName = name.Trim();

        using var conn = ConnectionFactory.CreateConnection();
        var existingNames = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT Name FROM Departments",
            cancellationToken: cancellationToken));
        if (existingNames.Any(d => string.Equals(d.Trim().ToTurkishLower(), cleanName.ToTurkishLower(), StringComparison.Ordinal)))
            throw new InvalidOperationException(Stokendra.LocalizationManager.L("dept_exists"));

        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO Departments (Name) VALUES (@Name)",
            new { Name = cleanName },
            cancellationToken: cancellationToken));
        LogAudit(conn, null, "Insert", "Departments", 0, cleanName);
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        string cleanName = name.Trim();

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            var inUse = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM StockMovements WHERE Department = @Name COLLATE NOCASE)",
                new { Name = cleanName },
                transaction: trans,
                cancellationToken: cancellationToken));
            if (inUse)
            {
                throw new InvalidOperationException(Stokendra.LocalizationManager.L("dept_in_use"));
            }

            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM Departments WHERE Name = @Name COLLATE NOCASE",
                new { Name = cleanName },
                transaction: trans,
                cancellationToken: cancellationToken));
            LogAudit(conn, (Microsoft.Data.Sqlite.SqliteTransaction)trans, "Delete", "Departments", 0, cleanName);
            await trans.CommitAsync(cancellationToken);
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;
        string cleanOld = oldName.Trim();
        string cleanNew = newName.Trim();

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!string.Equals(cleanOld.ToTurkishLower(), cleanNew.ToTurkishLower(), StringComparison.Ordinal))
            {
                var existingNames = await conn.QueryAsync<string>(new CommandDefinition(
                    "SELECT Name FROM Departments",
                    transaction: trans,
                    cancellationToken: cancellationToken));
                if (existingNames.Any(d => string.Equals(d.Trim().ToTurkishLower(), cleanNew.ToTurkishLower(), StringComparison.Ordinal)))
                    throw new InvalidOperationException(Stokendra.LocalizationManager.L("dept_exists"));
            }

            // 1. Update Departments table
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE Departments SET Name = @NewName WHERE Name = @OldName COLLATE NOCASE",
                new { NewName = cleanNew, OldName = cleanOld },
                transaction: trans,
                cancellationToken: cancellationToken));

            // 2. Update StockMovements table (Cascade)
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE StockMovements SET Department = @NewName WHERE Department = @OldName COLLATE NOCASE",
                new { NewName = cleanNew, OldName = cleanOld },
                transaction: trans,
                cancellationToken: cancellationToken));

            LogAudit(conn, (Microsoft.Data.Sqlite.SqliteTransaction)trans, "Update", "Departments", 0, $"{cleanOld} -> {cleanNew}");
            await trans.CommitAsync(cancellationToken);
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
