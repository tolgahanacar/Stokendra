using Avalonia;
using Avalonia.Controls;
using StokTakip.ViewModels;

namespace StokTakip.Views;

public partial class StocksView : UserControl
{
    public StocksView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is StocksViewModel vm)
        {
            _ = vm.LoadAsync();
        }
    }
}
