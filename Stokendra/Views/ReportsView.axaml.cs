using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Stokendra.ViewModels;
using ScottPlot;
using System.Linq;
using System.Collections.Generic;

namespace Stokendra.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ReportsViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(ReportsViewModel.ChartData))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateChart(vm.ChartData));
                }
            };
            
            if (vm.ChartData != null && vm.ChartData.Count > 0)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateChart(vm.ChartData));
        }
    }

    private void UpdateChart(List<(string Name, double Total)> data)
    {
        var chart = this.FindControl<ScottPlot.Avalonia.AvaPlot>("ConsumptionChart");
        if (chart == null) return;

        chart.Plot.Clear();
        
        if (data == null || data.Count == 0)
        {
            chart.Refresh();
            return;
        }

        double[] values = data.Select(x => x.Total).ToArray();
        double[] positions = Enumerable.Range(0, data.Count).Select(x => (double)x).ToArray();
        string[] labels = data.Select(x => x.Name.Length > 10 ? x.Name.Substring(0, 8) + ".." : x.Name).ToArray();

        var bars = chart.Plot.Add.Bars(positions, values);
        
        // ScottPlot 5 styling
        chart.Plot.Axes.Bottom.SetTicks(positions, labels);
        chart.Plot.Axes.Bottom.TickLabelStyle.Rotation = -45;
        chart.Plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
        
        // Dark theme adjustments
        chart.Plot.FigureBackground.Color = Color.FromHex("#161B27");
        chart.Plot.DataBackground.Color = Color.FromHex("#161B27");
        chart.Plot.Axes.Color(Color.FromHex("#94A3B8"));
        chart.Plot.Grid.LineColor = Color.FromHex("#1E2D45");
        
        chart.Refresh();
    }
}
