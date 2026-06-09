using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stokendra.Data.Interfaces;

public interface IDepartmentRepository
{
    Task<List<string>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
    Task UpdateAsync(string oldName, string newName, CancellationToken cancellationToken = default);
}
