using Avalonia.Controls;
using Avalonia.Input;
using Stokendra.ViewModels;
using Stokendra.Models;
using System.Linq;

namespace Stokendra.Views;

public partial class StockCardsView : UserControl
{
    public StockCardsView()
    {
        InitializeComponent();
        Unloaded += (s, e) =>
        {
            if (DataContext is StockCardsViewModel vm)
            {
                vm.SelectedCards.Clear();
                vm.SelectedCard = null;
            }
        };
    }

    private void CardsGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is StockCardsViewModel vm && sender is DataGrid grid)
        {
            vm.SelectedCards.ReplaceAll(grid.SelectedItems.OfType<StockCard>());
        }
    }

    private void CardsGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is StockCardsViewModel vm && vm.SelectedCard != null)
        {
            vm.ShowDetailCommand.Execute(null);
        }
    }
}
