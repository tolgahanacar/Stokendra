using Avalonia.Controls;
using StokTakip.ViewModels;
using System;

namespace StokTakip.Views;

public partial class BulkMovementView : Window
{
    public BulkMovementView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is BulkMovementViewModel vm)
        {
            vm.CloseAction = (result) => Close(result);
        }
    }
}
