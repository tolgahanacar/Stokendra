using Avalonia.Controls;
using Stokendra.ViewModels;
using System;

namespace Stokendra.Views;

public partial class PrintPreviewView : Window
{
    public PrintPreviewView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is PrintPreviewViewModel vm)
        {
            vm.CloseAction = () => Close();
        }
    }
}
