using Avalonia.Controls;
using Avalonia.Input;
using Stokendra.Models;
using Stokendra.ViewModels;
using System.Linq;

namespace Stokendra.Views;

public partial class ServicesView : UserControl
{
    public ServicesView()
    {
        InitializeComponent();
        Unloaded += (s, e) =>
        {
            if (DataContext is ServicesViewModel vm)
            {
                vm.SelectedRecords.Clear();
                vm.SelectedRecord = null;
            }
        };
        var grid = this.FindControl<DataGrid>("ServicesGrid");
        if (grid != null)
        {
            grid.SelectionChanged += (s, e) =>
            {
                if (DataContext is ServicesViewModel vm && grid.SelectedItems != null)
                {
                    vm.SelectedRecords.ReplaceAll(grid.SelectedItems.OfType<ServiceRecord>());
                }
            };
        }
    }

    private void ServicesGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ServicesViewModel vm && vm.CanEdit && vm.IsRecordSelected)
        {
            vm.EditCommand.Execute(null);
        }
    }
}
