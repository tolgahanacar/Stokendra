using Avalonia.Controls;
using Stokendra.Models;
using Stokendra.ViewModels;

namespace Stokendra.Views;

public partial class ServicesView : UserControl
{
    public ServicesView()
    {
        InitializeComponent();
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
}
