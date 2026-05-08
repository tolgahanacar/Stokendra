using Avalonia.Controls;
using StokTakip.ViewModels;
using System;

namespace StokTakip.Views;

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
