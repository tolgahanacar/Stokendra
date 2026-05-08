using Avalonia.Controls;
using Avalonia.Controls.Templates;
using StokTakip.ViewModels;
using System;
using System.IO;

namespace StokTakip;

public class ViewLocator : IDataTemplate
{
    public static Control? CreateView(object? data)
    {
        return new ViewLocator().Build(data);
    }

    public Control? Build(object? data)
    {
        if (data is null) return null;

        // StokTakip.ViewModels.DashboardViewModel → StokTakip.Views.DashboardView veya DashboardWindow
        var baseName = data.GetType().FullName!.Replace(".ViewModels.", ".Views.").Replace("ViewModel", "");
        var viewType = Type.GetType(baseName + "View") ?? Type.GetType(baseName + "Window");

        if (viewType is null)
        {
            return new TextBlock
            {
                Text = $"View bulunamadı: {baseName}View/Window",
                Foreground = Avalonia.Media.Brushes.OrangeRed
            };
        }

        try
        {
            var view = (Control)Activator.CreateInstance(viewType)!;
            view.DataContext = data;
            return view;
        }
        catch (Exception ex)
        {
            // İç exception zincirini çöz
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;

            // Dosyaya yaz — Avalonia'da Console.WriteLine görünmüyor
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Stokendra");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "viewlocator_error.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {viewType.Name}\n" +
                    $"  Root: {inner.GetType().Name}: {inner.Message}\n" +
                    $"  Full: {ex}\n\n");
            }
            catch { }

            return new TextBlock
            {
                Text = $"View oluşturulamadı: {viewType.Name}\n{inner.GetType().Name}: {inner.Message}",
                Foreground = Avalonia.Media.Brushes.OrangeRed,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(20)
            };
        }
    }

    public bool Match(object? data) => data is ViewModelBase;
}
