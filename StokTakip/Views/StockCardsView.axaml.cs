using Avalonia.Controls;
using System;
using StokTakip.ViewModels;

namespace StokTakip.Views;

public partial class StockCardsView : UserControl
{
    public StockCardsView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is StockCardsViewModel vm)
        {
            vm.ShowDialogAction = async (dialogVm) =>
            {
                var dialog = new AddStockCardWindow { DataContext = dialogVm };
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
        }
    }
}
