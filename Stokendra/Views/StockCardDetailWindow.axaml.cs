using Avalonia.Controls;
using Stokendra.ViewModels;
using System;

namespace Stokendra.Views;

public partial class StockCardDetailWindow : Window
{
    public StockCardDetailWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is StockCardDetailViewModel vm)
        {
            vm.CloseAction = (res) => this.Close(res);
        }
    }
}
