using Avalonia.Controls;
using Stokendra.ViewModels;

namespace Stokendra.Views;

public partial class BulkEditServicesView : Window
{
    public BulkEditServicesView()
    {
        InitializeComponent();
        DataContextChanged += (s, e) =>
        {
            if (DataContext is BulkEditServicesViewModel vm)
            {
                vm.CloseAction = (result) => Close(result);
            }
        };
    }
}
