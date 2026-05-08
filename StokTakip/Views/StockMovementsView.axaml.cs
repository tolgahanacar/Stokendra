using Avalonia.Controls;
using StokTakip.Models;
using StokTakip.ViewModels;

namespace StokTakip.Views;

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
                            if (item is StokHareketi h) vm.SelectedMovements.Add(h);
                        }
                    }
                }
            };
        }
    }
}
