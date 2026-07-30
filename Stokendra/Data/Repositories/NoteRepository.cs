using Dapper;
using Microsoft.Data.Sqlite;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System.Globalization;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Repositories;

public sealed class NoteRepository : RepositoryBase, INoteRepository
{
    public NoteRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<List<Note>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "SELECT Id, Date, Title, Content, CreatedAt, UpdatedAt FROM Notes ORDER BY CreatedAt DESC, Id DESC";
        var result = await conn.QueryAsync<Note>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(note.Title)) throw new InvalidOperationException("Title cannot be empty.");
        // note.CreatedAt is preserved from note if set, otherwise uses its default initialized value.
        note.UpdatedAt = DateTime.Now;

        using var conn = ConnectionFactory.CreateConnection();
        var sql = @"INSERT INTO Notes (Date, Title, Content, CreatedAt, UpdatedAt) 
                    VALUES (@Date, @Title, @Content, @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();";
        
        note.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new {
            Date = note.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            note.Title,
            Content = note.Content ?? "",
            CreatedAt = note.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            UpdatedAt = note.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        }, cancellationToken: cancellationToken));
        
        LogAudit("Insert", "Notes", note.Id, $"Title: {note.Title}");
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        note.UpdatedAt = DateTime.Now;
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "UPDATE Notes SET Title=@Title, Content=@Content, CreatedAt=@CreatedAt, UpdatedAt=@UpdatedAt WHERE Id=@Id";
        
        await conn.ExecuteAsync(new CommandDefinition(sql, new {
            note.Title,
            Content = note.Content ?? "",
            CreatedAt = note.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            UpdatedAt = note.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            note.Id
        }, cancellationToken: cancellationToken));
        
        LogAudit("Update", "Notes", note.Id, $"Title: {note.Title}");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM Notes WHERE Id=@Id", new { Id = id }, cancellationToken: cancellationToken));
        LogAudit("Delete", "Notes", id, "");
    }

    public async Task DeleteBulkAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            await conn.ExecuteAsync(new CommandDefinition("DELETE FROM Notes WHERE Id IN @Ids", new { Ids = idList }, trans, cancellationToken: cancellationToken));
            trans.Commit();
            LogAudit("Delete", "Notes", 0, "Bulk Note Delete");
        } catch { trans.Rollback(); throw; }
    }

    public async Task AddBulkAsync(IEnumerable<Note> notes, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            var sql = @"INSERT INTO Notes (Date, Title, Content, CreatedAt, UpdatedAt) 
                        VALUES (@Date, @Title, @Content, @CreatedAt, @UpdatedAt)";
            var data = notes.Select(n => new {
                Date = n.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Title = n.Title,
                Content = n.Content ?? "",
                CreatedAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            });
            await conn.ExecuteAsync(new CommandDefinition(sql, data, transaction: trans, cancellationToken: cancellationToken));
            await trans.CommitAsync(cancellationToken);
            LogAudit("Insert", "Notes", 0, "Bulk Insert");
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
