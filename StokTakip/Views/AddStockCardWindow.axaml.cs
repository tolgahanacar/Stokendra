using Avalonia.Controls;
using StokTakip.ViewModels;
using System;

namespace StokTakip.Views;

public partial class AddStockCardWindow : Window
{
    public AddStockCardWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is AddStockCardViewModel vm)
        {
            vm.CloseAction = (result) => Close(result);
        }
    }
}
