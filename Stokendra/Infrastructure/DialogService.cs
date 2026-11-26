using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using Stokendra.Views;

namespace Stokendra.Infrastructure;

public class DialogService : IDialogService
{
    private Window? GetActiveWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var activeModal = desktop.Windows.LastOrDefault(w => w.IsVisible && w.IsActive)
                              ?? desktop.Windows.LastOrDefault(w => w.IsVisible);
            return activeModal ?? desktop.MainWindow;
        }
        return null;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var window = GetActiveWindow();
        if (window == null) return;
        await MessageBoxWindow.Show(window, title, message);
    }

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var window = GetActiveWindow();
        if (window == null) return false;
        return await MessageBoxWindow.ShowConfirm(window, title, message);
    }

    private static (List<FilePickerFileType>? FileTypes, string? DefaultExt) ParseFilter(string? filter, string? fileName = null)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            string? extFromName = Path.GetExtension(fileName);
            return (null, string.IsNullOrEmpty(extFromName) ? null : extFromName.TrimStart('.'));
        }

        // Format expected: "Label (*.xlsx)|*.xlsx" or multiple pairs separated by '|'
        var parts = filter.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var types = new List<FilePickerFileType>();
        string? firstExt = null;

        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            var name = parts[i].Trim();
            var patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToArray();

            if (patterns.Length > 0)
            {
                types.Add(new FilePickerFileType(name) { Patterns = patterns });
                if (firstExt == null)
                {
                    var p = patterns[0].TrimStart('*').TrimStart('.');
                    if (!string.IsNullOrEmpty(p) && p != "*") firstExt = p;
                }
            }
        }

        if (firstExt == null && !string.IsNullOrEmpty(fileName))
            firstExt = Path.GetExtension(fileName).TrimStart('.');

        return (types.Count > 0 ? types : null, string.IsNullOrEmpty(firstExt) ? null : firstExt);
    }

    public async Task<string?> SaveFileAsync(string title, string defaultFileName, string filter)
    {
        var window = GetActiveWindow();
        if (window == null) return null;

        var (fileTypes, defaultExt) = ParseFilter(filter, defaultFileName);
        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            DefaultExtension = defaultExt,
            FileTypeChoices = fileTypes
        };

        var file = await window.StorageProvider.SaveFilePickerAsync(options);
        return file?.Path.LocalPath;
    }

    public async Task<string?> OpenFileAsync(string title, string filter)
    {
        var window = GetActiveWindow();
        if (window == null) return null;

        var (fileTypes, _) = ParseFilter(filter);
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        };

        var files = await window.StorageProvider.OpenFilePickerAsync(options);
        return files.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> OpenFolderAsync(string title)
    {
        var window = GetActiveWindow();
        if (window == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var folders = await window.StorageProvider.OpenFolderPickerAsync(options);
        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<bool> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        var window = GetActiveWindow();
        if (window == null) return false;

        var view = ViewLocator.CreateView(viewModel);
        if (view is Window dialogWindow)
        {
            dialogWindow.DataContext = viewModel;
            return await dialogWindow.ShowDialog<bool>(window);
        }
        else if (view is TemplatedControl templatedControl)
        {
            var wrapper = new Window
            {
                Content = templatedControl,
                DataContext = viewModel,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                Background = templatedControl.Background ?? Avalonia.Media.Brushes.Transparent
            };
            return await wrapper.ShowDialog<bool>(window);
        }
        else if (view is TextBlock tb)
        {
            AppLogger.LogError($"Dialog view resolution failed for {typeof(TViewModel).Name}: {tb.Text}");
            await MessageBoxWindow.Show(window, LocalizationManager.L("error"), tb.Text ?? "View resolution failed.");
        }
        
        return false;
    }
}
