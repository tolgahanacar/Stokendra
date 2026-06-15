using Avalonia.Controls;
using System.Threading.Tasks;

namespace Stokendra.Views;

public partial class MessageBoxWindow : Window
{
    public MessageBoxWindow()
    {
        InitializeComponent();
    }

    public static async Task Show(Window parent, string title, string message)
    {
        var msg = new MessageBoxWindow();
        msg.Title = title;
        msg.FindControl<TextBlock>("TitleText")!.Text = title;
        msg.FindControl<TextBlock>("MessageText")!.Text = message;
        msg.FindControl<Button>("OkBtn")!.Click += (_, _) => msg.Close();
        await msg.ShowDialog(parent);
    }

    public static async Task<bool> ShowConfirm(Window parent, string title, string message)
    {
        var msg = new MessageBoxWindow();
        msg.Title = title;
        msg.FindControl<TextBlock>("TitleText")!.Text = title;
        msg.FindControl<TextBlock>("MessageText")!.Text = message;
        
        var cancelBtn = msg.FindControl<Button>("CancelBtn")!;
        cancelBtn.IsVisible = true;
        
        bool result = false;
        msg.FindControl<Button>("OkBtn")!.Click += (_, _) => { result = true; msg.Close(); };
        cancelBtn.Click += (_, _) => { result = false; msg.Close(); };
        
        await msg.ShowDialog(parent);
        return result;
    }
}
