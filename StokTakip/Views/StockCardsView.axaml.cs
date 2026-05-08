using Avalonia.Controls;
using StokTakip.ViewModels;
using StokTakip.Models;

namespace StokTakip.Views;

public partial class StockCardsView : UserControl
{
    public StockCardsView() => InitializeComponent();

    private void CardsGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is StockCardsViewModel vm)
        {
            foreach (var item in e.RemovedItems)
                if (item is StokKarti k) vm.SelectedCards.Remove(k);
            
            foreach (var item in e.AddedItems)
                if (item is StokKarti k && !vm.SelectedCards.Contains(k))
                    vm.SelectedCards.Add(k);
        }
    }
}
