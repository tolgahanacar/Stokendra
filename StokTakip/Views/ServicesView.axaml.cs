using Avalonia.Controls;
using Avalonia.Platform.Storage;
using StokTakip.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StokTakip.Views;

public partial class ServicesView : UserControl
{
    public ServicesView()
    {
        InitializeComponent();
        DataContextChanged += (s, e) =>
        {
            if (DataContext is ServicesViewModel vm)
            {
                vm.ShowDialogAction = async (subVm) =>
                {
                    var window = new AddServiceRecordWindow { DataContext = subVm };
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel is Window parent)
                        return await window.ShowDialog<bool>(parent);
                    return false;
                };

                vm.SaveFileAction = async (name, filter) =>
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel == null) return null;

                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Excel Olarak Kaydet",
                        SuggestedFileName = name,
                        DefaultExtension = "xlsx"
                    });
                    return file?.Path.LocalPath;
                };

                vm.OpenFileAction = async (filter) =>
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel == null) return null;

                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Excel Dosyası Seç",
                        AllowMultiple = false,
                        FileTypeFilter = new[] { new FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } } }
                    });
                    return files.FirstOrDefault()?.Path.LocalPath;
                };
            }
        };
    }

    private void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is ServicesViewModel vm && vm.IsRecordSelected)
        {
            vm.EditCommand.Execute(null);
        }
    }
}
