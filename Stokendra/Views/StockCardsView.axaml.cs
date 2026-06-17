using Avalonia.Controls;
using Stokendra.ViewModels;
using Stokendra.Models;

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
        if (DataContext is StockCardsViewModel vm)
        {
            vm.SelectedCards.Clear();
            if (sender is DataGrid grid)
            {
                foreach (var item in grid.SelectedItems)
                {
                    if (item is StockCard k)
                    {
                        vm.SelectedCards.Add(k);
                    }
                }
            }
        }
    }
}
