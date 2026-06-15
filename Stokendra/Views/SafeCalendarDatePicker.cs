using Avalonia.Controls;
using Avalonia.Input;

namespace Stokendra.Views;

public class SafeCalendarDatePicker : CalendarDatePicker
{
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (IsKeyboardFocusWithin)
        {
            base.OnPointerWheelChanged(e);
        }
        // If focus is not within the control, do not call the base class handler.
        // This prevents the date from changing, and by not setting e.Handled = true,
        // it allows the pointer wheel event to bubble up to the parent ScrollViewer/DataGrid.
    }
}
