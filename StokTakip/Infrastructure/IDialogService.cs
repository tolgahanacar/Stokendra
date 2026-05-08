using System.Threading.Tasks;

namespace StokTakip.Infrastructure;

public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);
    Task<bool> ShowConfirmAsync(string title, string message);
    Task<string?> SaveFileAsync(string title, string defaultFileName, string filter);
    Task<string?> OpenFileAsync(string title, string filter);
    Task<string?> OpenFolderAsync(string title);
    Task<bool> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class;
}
