using Avalonia.Controls;
using StokTakip.ViewModels;
using System;

namespace StokTakip.Views;

public partial class AddMovementWindow : Window
{
    public AddMovementWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is AddMovementViewModel vm)
        {
            vm.CloseAction = (result) => Close(result);
        }
    }
}
