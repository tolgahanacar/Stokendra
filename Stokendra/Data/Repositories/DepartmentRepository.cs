using Dapper;
using Stokendra.Data.Interfaces;
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

        using var conn = ConnectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO Departments (Name) VALUES (@Name)",
            new { Name = name.Trim() },
            cancellationToken: cancellationToken));
        LogAudit("Insert", "Departments", 0, name.Trim());
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        using var conn = ConnectionFactory.CreateConnection();
        var inUse = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM StockMovements WHERE Department = @Name)",
            new { Name = name.Trim() },
            cancellationToken: cancellationToken));
        if (inUse)
        {
            throw new InvalidOperationException(Stokendra.LocalizationManager.L("dept_in_use"));
        }

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Departments WHERE Name = @Name",
            new { Name = name.Trim() },
            cancellationToken: cancellationToken));
        LogAudit("Delete", "Departments", 0, name.Trim());
    }

    public async Task UpdateAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Update Departments table
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE Departments SET Name = @NewName WHERE Name = @OldName",
                new { NewName = newName.Trim(), OldName = oldName.Trim() },
                transaction: trans,
                cancellationToken: cancellationToken));

            // 2. Update StockMovements table (Cascade)
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE StockMovements SET Department = @NewName WHERE Department = @OldName",
                new { NewName = newName.Trim(), OldName = oldName.Trim() },
                transaction: trans,
                cancellationToken: cancellationToken));

            await trans.CommitAsync(cancellationToken);
            LogAudit("Update", "Departments", 0, $"{oldName.Trim()} -> {newName.Trim()}");
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
