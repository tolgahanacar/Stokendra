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

    public List<Note> GetAll()
    {
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "SELECT Id, Date, Title, Content, CreatedAt, UpdatedAt FROM Notes ORDER BY CreatedAt DESC, Id DESC";
        return conn.Query<Note>(sql).ToList();
    }

    public async Task<List<Note>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "SELECT Id, Date, Title, Content, CreatedAt, UpdatedAt FROM Notes ORDER BY CreatedAt DESC, Id DESC";
        var result = await conn.QueryAsync<Note>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public void Add(Note note)
    {
        if (string.IsNullOrWhiteSpace(note.Title)) throw new InvalidOperationException("Title cannot be empty.");
        note.CreatedAt = DateTime.Now;
        note.UpdatedAt = DateTime.Now;

        using var conn = ConnectionFactory.CreateConnection();
        var sql = @"INSERT INTO Notes (Date, Title, Content, CreatedAt, UpdatedAt) 
                    VALUES (@Date, @Title, @Content, @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();";
        
        note.Id = conn.ExecuteScalar<int>(sql, new {
            Date = note.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            note.Title,
            Content = note.Content ?? "",
            CreatedAt = note.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            UpdatedAt = note.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
        
        LogAudit("Insert", "Notes", note.Id, $"Title: {note.Title}");
    }

    public void Update(Note note)
    {
        note.UpdatedAt = DateTime.Now;
        using var conn = ConnectionFactory.CreateConnection();
        var sql = "UPDATE Notes SET Title=@Title, Content=@Content, UpdatedAt=@UpdatedAt WHERE Id=@Id";
        
        conn.Execute(sql, new {
            note.Title,
            Content = note.Content ?? "",
            UpdatedAt = note.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            note.Id
        });
        
        LogAudit("Update", "Notes", note.Id, $"Title: {note.Title}");
    }

    public void Delete(int id)
    {
        using var conn = ConnectionFactory.CreateConnection();
        conn.Execute("DELETE FROM Notes WHERE Id=@Id", new { Id = id });
        LogAudit("Delete", "Notes", id, "");
    }

    public void DeleteBulk(IEnumerable<int> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        using var conn = ConnectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();
        try {
            conn.Execute("DELETE FROM Notes WHERE Id IN @Ids", new { Ids = idList }, trans);
            trans.Commit();
            LogAudit("Delete", "Notes", 0, "Bulk Note Delete");
        } catch { trans.Rollback(); throw; }
    }
}
