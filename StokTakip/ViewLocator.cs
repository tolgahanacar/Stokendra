using Avalonia.Controls;
using Avalonia.Controls.Templates;
using StokTakip.Infrastructure;
using StokTakip.ViewModels;
using System;

namespace StokTakip;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;

        // StokTakip.ViewModels.DashboardViewModel → StokTakip.Views.DashboardView
        var viewTypeName = data.GetType().FullName!
            .Replace(".ViewModels.", ".Views.")
            .Replace("ViewModel", "View");

        var viewType = Type.GetType(viewTypeName);
        if (viewType is null)
        {
            return new TextBlock
            {
                Text = $"View bulunamadı: {viewTypeName}",
                Foreground = Avalonia.Media.Brushes.OrangeRed
            };
        }

        try
        {
            // View'ı parametresiz constructor ile oluştur
            var view = (Control)Activator.CreateInstance(viewType)!;
            // DataContext'i ViewModel olarak set et
            view.DataContext = data;
            return view;
        }
        catch (Exception ex)
        {
            // İç exception'ı bul (özellikle TargetInvocationException durumunda)
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;

            var msg = $"View oluşturulamadı: {viewType.Name}\n{inner.GetType().Name}: {inner.Message}";
            Console.WriteLine($"[ViewLocator ERROR] {viewType.Name}: {ex}");
            return new TextBlock
            {
                Text = msg,
                Foreground = Avalonia.Media.Brushes.OrangeRed,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(20)
            };
        }
    }

    public bool Match(object? data) => data is ViewModelBase;
}
