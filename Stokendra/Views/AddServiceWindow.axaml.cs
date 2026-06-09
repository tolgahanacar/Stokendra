using Avalonia.Controls;
using Stokendra.ViewModels;
using System;

namespace Stokendra.Views;

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
