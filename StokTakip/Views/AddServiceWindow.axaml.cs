using Avalonia.Controls;
using StokTakip.ViewModels;
using System;

namespace StokTakip.Views;

public partial class AddServiceWindow : Window
{
    public AddServiceWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is AddServiceViewModel vm)
        {
            vm.CloseAction = result => Close(result);
        }
    }
}
