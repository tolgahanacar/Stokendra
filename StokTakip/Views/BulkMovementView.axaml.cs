using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StokTakip.ViewModels;
using System;

namespace StokTakip.Views;

public partial class BulkMovementView : Window
{
    public BulkMovementView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is BulkMovementViewModel vm)
        {
            vm.CloseAction = (result) => Close(result);
        }
    }

    /// <summary>
    /// DataGrid satırına tıklandığında ilgili öğenin IsSelected durumunu toggle eder.
    /// Kullanıcı artık sadece checkbox'a değil, satırın herhangi yerine tıklayabilir.
    /// </summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // NumericUpDown (miktar alanı) veya butonlar üzerinde yapılan tıklamalar
        // toggle işlemini tetiklememeli
        if (e.Source is Avalonia.Visual visual)
        {
            var current = visual;
            while (current != null)
            {
                if (current is NumericUpDown || current is Button || current is RepeatButton)
                    return;
                current = current.GetVisualParent() as Avalonia.Visual;
            }
        }

        // DataGridRow bulunursa toggle yap
        if (e.Source is Avalonia.Visual source)
        {
            var current = source;
            while (current != null)
            {
                if (current is DataGridRow row && row.DataContext is BulkMovementItemViewModel item)
                {
                    item.IsSelected = !item.IsSelected;
                    return;
                }
                current = current.GetVisualParent() as Avalonia.Visual;
            }
        }
    }
}
