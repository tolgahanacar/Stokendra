using Stokendra.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for StockCard CRUD operations.
/// </summary>
public interface IStockCardRepository
{
    /// <summary>Gets all stock cards.</summary>
    List<StockCard> GetAll();

    /// <summary>Gets all stock cards asynchronously.</summary>
    Task<List<StockCard>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets only parent cards (CardType = Parent).</summary>
    List<StockCard> GetParentCards();

    /// <summary>Gets only parent cards asynchronously.</summary>
    Task<List<StockCard>> GetParentCardsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets child cards. If parentId is provided, returns only child cards linked to that parent.</summary>
    List<StockCard> GetChildCards(int? parentId = null);

    /// <summary>Gets child cards asynchronously.</summary>
    Task<List<StockCard>> GetChildCardsAsync(int? parentId = null, CancellationToken cancellationToken = default);

    /// <summary>Gets low stock child cards directly from the database.</summary>
    Task<List<StockCard>> GetLowStockCardsAsync(int limit, int fallbackThreshold = 3, CancellationToken cancellationToken = default);

    /// <summary>Gets the stock card with the specified ID. Returns null if not found.</summary>
    StockCard? GetById(int id);

    /// <summary>Adds a new stock card. The generated ID is assigned to stockCard.Id.</summary>
    void Add(StockCard stockCard);

    /// <summary>Adds a new stock card asynchronously.</summary>
    Task AddAsync(StockCard stockCard, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing stock card.</summary>
    void Update(StockCard stockCard);

    /// <summary>Deletes the stock card with the specified ID. Cascade deletes linked movements.</summary>
    void Delete(int id);

    /// <summary>Generates the next sequential stock code (e.g. "042").</summary>
    string GetNextCode();
}
