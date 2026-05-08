using Avalonia.Controls;
using StokTakip.ViewModels;
using System;

namespace StokTakip.Views;

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
