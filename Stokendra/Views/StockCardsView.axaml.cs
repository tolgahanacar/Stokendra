using Avalonia.Controls;
using Stokendra.ViewModels;
using Stokendra.Models;

namespace Stokendra.Views;

public partial class StockCardsView : UserControl
{
    public StockCardsView() => InitializeComponent();

    private void CardsGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is StockCardsViewModel vm)
        {
            foreach (var item in e.RemovedItems)
                if (item is StockCard k) vm.SelectedCards.Remove(k);
            
            foreach (var item in e.AddedItems)
                if (item is StockCard k && !vm.SelectedCards.Contains(k))
                    vm.SelectedCards.Add(k);
        }
    }
}
