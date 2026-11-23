using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Stokendra.ViewModels;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq.Expressions;

namespace Stokendra;

public class ViewLocator : IDataTemplate
{
    private static readonly ConcurrentDictionary<Type, Func<Control>?> _factoryCache = new();

    public static Control? CreateView(object? data)
    {
        return new ViewLocator().Build(data);
    }

    public Control? Build(object? data)
    {
        if (data is null) return null;

        var vmType = data.GetType();
        var factory = _factoryCache.GetOrAdd(vmType, type =>
        {
            var baseName = type.FullName!.Replace(".ViewModels.", ".Views.").Replace("ViewModel", "");
            var viewType = Type.GetType(baseName + "View") ?? Type.GetType(baseName + "Window");
            if (viewType is null) return null;

            try
            {
                var newExp = Expression.New(viewType);
                var lambda = Expression.Lambda<Func<Control>>(newExp);
                return lambda.Compile();
            }
            catch
            {
                return () => (Control)Activator.CreateInstance(viewType)!;
            }
        });

        if (factory is null)
        {
            var baseName = vmType.FullName!.Replace(".ViewModels.", ".Views.").Replace("ViewModel", "");
            return new TextBlock
            {
                Text = $"View bulunamadı: {baseName}View/Window",
                Foreground = Avalonia.Media.Brushes.OrangeRed
            };
        }

        try
        {
            var view = factory();
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
                    $"[{DateTime.Now:HH:mm:ss}] {vmType.Name}\n" +
                    $"  Root: {inner.GetType().Name}: {inner.Message}\n" +
                    $"  Full: {ex}\n\n");
            }
            catch (Exception logEx) { AppLogger.LogError("ViewLocator: failed to write error log", logEx); }

            return new TextBlock
            {
                Text = $"View oluşturulamadı: {vmType.Name}\n{inner.GetType().Name}: {inner.Message}",
                Foreground = Avalonia.Media.Brushes.OrangeRed,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(20)
            };
        }
    }

    public bool Match(object? data) => data is ViewModelBase;
}
