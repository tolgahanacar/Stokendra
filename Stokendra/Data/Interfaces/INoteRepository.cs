using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stokendra.Models;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for Note CRUD operations.
/// </summary>
public interface INoteRepository
{
    /// <summary>Gets all notes ordered by date descending.</summary>
    List<Note> GetAll();

    /// <summary>Gets all notes asynchronously.</summary>
    Task<List<Note>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a new note. The generated ID is assigned to note.Id.</summary>
    void Add(Note note);

    /// <summary>Updates an existing note.</summary>
    void Update(Note note);

    /// <summary>Deletes the note with the specified ID.</summary>
    void Delete(int id);

    /// <summary>Deletes multiple notes in bulk.</summary>
    void DeleteBulk(IEnumerable<int> ids);
}
