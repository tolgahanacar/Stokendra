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
                if (DataContext is StockMovementsViewModel vm)
                {
                    vm.SelectedMovements.Clear();
                    if (grid.SelectedItems != null)
                    {
                        foreach (var item in grid.SelectedItems)
                        {
                            if (item is StockMovement h) vm.SelectedMovements.Add(h);
                        }
                    }
                }
            };
        }
    }
}
