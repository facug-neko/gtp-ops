using System.Windows;
using System.Windows.Controls;
using AxiomOps.UI.ViewModels;

namespace AxiomOps.UI.Views;

public partial class LogsView : UserControl
{
    private LogsViewModel? _viewModel;

    public LogsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        if (DataContext is LogsViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.ScrolledToEndRequested += ScrollToEnd;
        }
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.ScrolledToEndRequested -= ScrollToEnd;
            _viewModel = null;
        }
    }

    // Tail/refresh asks the view to jump to the newest line.
    private void ScrollToEnd()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (LinesList.Items.Count > 0)
            {
                LinesList.ScrollIntoView(LinesList.Items[^1]);
            }
        });
    }
}
