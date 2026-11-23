using Avalonia.Controls;
using Stokendra.Models;
using Stokendra.ViewModels;

namespace Stokendra.Views;

public partial class StockMovementsView : UserControl
{
    public StockMovementsView()
    {
        InitializeComponent();
        var grid = this.FindControl<DataGrid>("MovementsGrid");
        if (grid != null)
        {
            grid.SelectionChanged += (s, e) =>
            {
                if (DataContext is StockMovementsViewModel vm && grid.SelectedItems != null)
                {
                    vm.SelectedMovements.ReplaceAll(grid.SelectedItems.OfType<StockMovement>());
                }
            };
        }
    }
}
