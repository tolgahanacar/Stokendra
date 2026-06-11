using Dapper;
using Stokendra.Data.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Repositories;

public sealed class DepartmentRepository(IDbConnectionFactory connectionFactory) : IDepartmentRepository
{
    public async Task<List<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var conn = connectionFactory.CreateConnection();
        var result = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT Name FROM Departments ORDER BY Name", 
            cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task AddAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        
        using var conn = connectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO Departments (Name) VALUES (@Name)", 
            new { Name = name.Trim() }, 
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        using var conn = connectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Departments WHERE Name = @Name", 
            new { Name = name.Trim() }, 
            cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;

        using var conn = connectionFactory.CreateConnection();
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
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
