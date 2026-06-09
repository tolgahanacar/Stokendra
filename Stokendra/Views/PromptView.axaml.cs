using Avalonia.Controls;
using Stokendra.ViewModels;
using System;

namespace Stokendra.Views;

public partial class PromptView : Window
{
    public PromptView()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is PromptViewModel vm)
        {
            vm.CloseAction = (result) => Close(result);
        }
    }
}
