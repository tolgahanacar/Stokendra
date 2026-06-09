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
    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window == null) return;
        await MessageBoxWindow.Show(window, title, message);
    }

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window == null) return false;
        return await MessageBoxWindow.ShowConfirm(window, title, message);
    }

    public async Task<string?> SaveFileAsync(string title, string defaultFileName, string filter)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName
        };

        var file = await window.StorageProvider.SaveFilePickerAsync(options);
        return file?.Path.LocalPath;
    }

    public async Task<string?> OpenFileAsync(string title, string filter)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var files = await window.StorageProvider.OpenFilePickerAsync(options);
        return files.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> OpenFolderAsync(string title)
    {
        var window = GetMainWindow();
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
        var window = GetMainWindow();
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
        
        return false;
    }
}
