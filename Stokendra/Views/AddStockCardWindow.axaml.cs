using Avalonia.Controls;
using Stokendra.ViewModels;
using System;

namespace Stokendra.Views;

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
