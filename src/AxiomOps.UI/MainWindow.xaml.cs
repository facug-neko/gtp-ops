using System.Windows;
using AxiomOps.UI.ViewModels;

namespace AxiomOps.UI;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
