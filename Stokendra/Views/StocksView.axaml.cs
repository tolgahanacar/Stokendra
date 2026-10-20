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
            Infrastructure.AsyncHelper.RunSafe(
                () => vm.LoadAsync(),
                ex => AppLogger.LogError("StocksView load failed", ex));
        }
    }
}
