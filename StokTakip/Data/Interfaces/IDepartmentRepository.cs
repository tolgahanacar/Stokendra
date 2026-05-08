using System.Collections.Generic;
using System.Threading.Tasks;

namespace StokTakip.Data.Interfaces;

public interface IDepartmentRepository
{
    Task<List<string>> GetAllAsync();
    Task AddAsync(string name);
    Task DeleteAsync(string name);
    Task UpdateAsync(string oldName, string newName);
}
