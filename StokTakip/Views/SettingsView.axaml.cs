using Avalonia.Controls;
using Avalonia.Platform.Storage;
using StokTakip.ViewModels;
using System;
using System.Threading.Tasks;

namespace StokTakip.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        
        DataContextChanged += (s, e) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.SaveFileAction = async (name, filter) =>
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel == null) return null;

                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Yedek Dosyası Kaydet",
                        SuggestedFileName = name,
                        DefaultExtension = "xlsx"
                    });
                    return file?.Path.LocalPath;
                };
            }
        };
    }
}
