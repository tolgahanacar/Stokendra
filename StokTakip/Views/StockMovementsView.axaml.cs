using Avalonia.Controls;
using System;
using StokTakip.ViewModels;

namespace StokTakip.Views;

public partial class StockMovementsView : UserControl
{
    public StockMovementsView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is StockMovementsViewModel vm)
        {
            vm.ShowDialogAction = async (dialogVm) =>
            {
                var dialog = new AddMovementWindow { DataContext = dialogVm };
                var mainWindow = (VisualRoot as Window);
                return await dialog.ShowDialog<bool>(mainWindow!);
            };

            vm.SaveFileAction = async (fileName, filter) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return null;

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Excel Kaydet",
                    SuggestedFileName = fileName
                });

                return file?.Path.LocalPath;
            };

            vm.OpenFileAction = async (filter) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return null;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Excel Aç",
                    AllowMultiple = false
                });

                return files.Count > 0 ? files[0].Path.LocalPath : null;
            };
        }
    }

    private void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is StockMovementsViewModel vm && vm.IsMovementSelected)
        {
            vm.EditMovementCommand.Execute(null);
        }
    }
}
