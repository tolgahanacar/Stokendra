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

    public async Task<List<Note>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        const string sql = "SELECT Id, Date, Title, Content, CreatedAt, UpdatedAt FROM Notes ORDER BY CreatedAt DESC, Id DESC LIMIT @Limit OFFSET @Offset";
        var result = await conn.QueryAsync<Note>(new CommandDefinition(
            sql,
            new { Limit = pageSize, Offset = Math.Max(0, (page - 1) * pageSize) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result.ToList();
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Notes",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(note.Title)) throw new InvalidOperationException(LocalizationManager.L("title_required"));
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
        
        LogAudit(conn, null, "Insert", "Notes", note.Id, $"Title: {note.Title}");
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(note.Title)) throw new ArgumentException(LocalizationManager.L("title_required"));

        note.UpdatedAt = DateTime.Now;
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "UPDATE Notes SET Date=@Date, Title=@Title, Content=@Content, CreatedAt=@CreatedAt, UpdatedAt=@UpdatedAt WHERE Id=@Id";
        
        await conn.ExecuteAsync(new CommandDefinition(sql, new {
            Date = note.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            note.Title,
            Content = note.Content ?? "",
            CreatedAt = note.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            UpdatedAt = note.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            note.Id
        }, cancellationToken: cancellationToken));
        
        LogAudit(conn, null, "Update", "Notes", note.Id, $"Title: {note.Title}");
    }

    public async Task UpdateBulkAsync(IEnumerable<Note> notes, CancellationToken cancellationToken = default)
    {
        var list = notes.ToList();
        if (list.Count == 0) return;

        foreach (var n in list)
        {
            if (string.IsNullOrWhiteSpace(n.Title))
                throw new ArgumentException(LocalizationManager.L("title_required"));
        }

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var sql = "UPDATE Notes SET Date=@Date, Title=@Title, Content=@Content, CreatedAt=@CreatedAt, UpdatedAt=@UpdatedAt WHERE Id=@Id";
            var payload = list.Select(n => new {
                Date = n.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Title = n.Title.Trim(),
                Content = n.Content ?? "",
                CreatedAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                UpdatedAt = nowStr,
                n.Id
            });
            await conn.ExecuteAsync(new CommandDefinition(sql, payload, transaction: trans, cancellationToken: cancellationToken)).ConfigureAwait(false);
            LogAudit(conn, (SqliteTransaction)trans, "Update", "Notes", 0, $"Bulk Note Update ({list.Count})");
            await trans.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM Notes WHERE Id=@Id", new { Id = id }, cancellationToken: cancellationToken));
        LogAudit(conn, null, "Delete", "Notes", id, "");
    }

    public async Task DeleteBulkAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try {
            await conn.ExecuteAsync(new CommandDefinition("DELETE FROM Notes WHERE Id IN @Ids", new { Ids = idList }, trans, cancellationToken: cancellationToken));
            LogAudit(conn, (SqliteTransaction)trans, "Delete", "Notes", 0, "Bulk Note Delete");
            await trans.CommitAsync(cancellationToken);
        } catch { await trans.RollbackAsync(cancellationToken); throw; }
    }

    public async Task AddBulkAsync(IEnumerable<Note> notes, CancellationToken cancellationToken = default)
    {
        var list = notes.ToList();
        if (list.Count == 0) return;
        foreach (var n in list)
        {
            if (string.IsNullOrWhiteSpace(n.Title))
                throw new InvalidOperationException(LocalizationManager.L("title_required"));
        }

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            var sql = @"INSERT INTO Notes (Date, Title, Content, CreatedAt, UpdatedAt) 
                        VALUES (@Date, @Title, @Content, @CreatedAt, @UpdatedAt)";
            var data = list.Select(n => new {
                Date = n.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Title = n.Title.Trim(),
                Content = n.Content ?? "",
                CreatedAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            });
            await conn.ExecuteAsync(new CommandDefinition(sql, data, transaction: trans, cancellationToken: cancellationToken));
            LogAudit(conn, (SqliteTransaction)trans, "Insert", "Notes", 0, $"Bulk Insert ({list.Count})");
            await trans.CommitAsync(cancellationToken);
        }
        catch
        {
            await trans.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
