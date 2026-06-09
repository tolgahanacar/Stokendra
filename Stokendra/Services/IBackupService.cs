using System.Threading.Tasks;

namespace Stokendra.Services;

public interface IBackupService
{
    Task CheckAutoBackupAsync();
    Task<string> PerformBackupAsync(string destFolder);
    Task<string> ExportAllExcelAsync(string destFolder);
}
