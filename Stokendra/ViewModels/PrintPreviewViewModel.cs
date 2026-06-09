using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class PrintPreviewViewModel : ViewModelBase
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _content;
    [ObservableProperty] private string _statusText = "Düzenledikten sonra 'Tarayıcıda Aç' diyerek yazdırabilirsiniz.";

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
            
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"Hata: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Cancel() => CloseAction?.Invoke();
}
