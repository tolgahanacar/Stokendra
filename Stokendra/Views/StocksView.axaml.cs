using Avalonia;
using Avalonia.Controls;
using Stokendra.ViewModels;

namespace Stokendra.Views;

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
