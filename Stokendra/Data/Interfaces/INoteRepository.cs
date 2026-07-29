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
    /// <summary>Gets all notes asynchronously.</summary>
    Task<List<Note>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a new note asynchronously. The generated ID is assigned to note.Id.</summary>
    Task AddAsync(Note note, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing note asynchronously.</summary>
    Task UpdateAsync(Note note, CancellationToken cancellationToken = default);

    /// <summary>Deletes the note with the specified ID asynchronously.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple notes in bulk asynchronously.</summary>
    Task DeleteBulkAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
}
