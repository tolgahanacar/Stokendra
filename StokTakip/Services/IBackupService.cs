using System.Threading.Tasks;

namespace StokTakip.Services;

public interface IBackupService
{
    Task CheckAutoBackupAsync();
    Task<string> PerformBackupAsync(string destFolder);
    Task<string> ExportAllExcelAsync(string destFolder);
}
