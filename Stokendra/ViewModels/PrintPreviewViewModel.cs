using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class PrintPreviewViewModel : ViewModelBase
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _content;
    [ObservableProperty] private string _statusText = LocalizationManager.L("print_preview_status");

    public Action? CloseAction { get; set; }

    public PrintPreviewViewModel(string title, string content)
    {
        _title = title;
        _content = content;
    }

    [RelayCommand]
    public async Task OpenInBrowser()
    {
        try
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"{Title.Replace(" ", "_")}_{DateTime.Now:HHmm}.html");
            await File.WriteAllTextAsync(tempPath, Content);
            
            var psi = new System.Diagnostics.ProcessStartInfo { FileName = tempPath, UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
            
            // Fire-and-forget deletion after a delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000); // 10 seconds is plenty of time for the browser to read the file
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { }
            });
            
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Cancel() => CloseAction?.Invoke();
}
