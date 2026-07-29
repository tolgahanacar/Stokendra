using Avalonia.Controls;
using Stokendra.ViewModels;
using System;

namespace Stokendra.Views;

public partial class BulkEditNotesView : Window
{
    public BulkEditNotesView()
    {
        InitializeComponent();
        DataContextChanged += (s, e) =>
        {
            if (DataContext is BulkEditNotesViewModel vm)
            {
                vm.CloseAction = (result) => Close(result);
            }
        };
    }
}
