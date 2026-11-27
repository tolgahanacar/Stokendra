using Avalonia.Controls;
using Avalonia.Input;
using Stokendra.Models;
using Stokendra.ViewModels;
using System.Linq;

namespace Stokendra.Views;

public partial class StockMovementsView : UserControl
{
    public StockMovementsView()
    {
        InitializeComponent();
        Unloaded += (s, e) =>
        {
            if (DataContext is StockMovementsViewModel vm)
            {
                vm.SelectedMovements.Clear();
                vm.SelectedMovement = null;
            }
        };
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

    private void MovementsGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is StockMovementsViewModel vm && vm.CanEdit && vm.IsMovementSelected)
        {
            vm.EditMovementCommand.Execute(null);
        }
    }
}
