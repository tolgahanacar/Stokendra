using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Stokendra.ViewModels;
using ScottPlot;
using System;
using System.ComponentModel;
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

    private ReportsViewModel? _viewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _viewModel = DataContext as ReportsViewModel;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            UpdateChart(_viewModel?.ChartData ?? new List<(string Name, double Total)>()));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportsViewModel.ChartData) && _viewModel != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateChart(_viewModel.ChartData));
        }
    }

    private static void ApplyChartDarkTheme(ScottPlot.Avalonia.AvaPlot chart)
    {
        chart.Plot.FigureBackground.Color = Color.FromHex("#161B27");
        chart.Plot.DataBackground.Color = Color.FromHex("#161B27");
        chart.Plot.Axes.Color(Color.FromHex("#94A3B8"));
        chart.Plot.Grid.LineColor = Color.FromHex("#1E2D45");
    }

    private void UpdateChart(List<(string Name, double Total)> data)
    {
        var chart = this.FindControl<ScottPlot.Avalonia.AvaPlot>("ConsumptionChart");
        if (chart == null) return;

        chart.Plot.Clear();
        ApplyChartDarkTheme(chart);

        if (data == null || data.Count == 0)
        {
            chart.Plot.Axes.AutoScale();
            chart.Refresh();
            return;
        }

        double[] values = data.Select(x => x.Total).ToArray();
        double[] positions = Enumerable.Range(0, data.Count).Select(x => (double)x).ToArray();
        string[] labels = data.Select(x => x.Name.Length > 20 ? x.Name.Substring(0, 18) + ".." : x.Name).ToArray();

        chart.Plot.Add.Bars(positions, values);

        // ScottPlot 5 styling
        chart.Plot.Axes.Bottom.SetTicks(positions, labels);
        chart.Plot.Axes.Bottom.TickLabelStyle.Rotation = -45;
        chart.Plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
        chart.Plot.Axes.Bottom.MinimumSize = 60;

        chart.Plot.Axes.AutoScale();
        chart.Refresh();
    }
}
