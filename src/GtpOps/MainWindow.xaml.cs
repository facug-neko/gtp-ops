using System.Windows;
using GtpOps.ViewModels;

namespace GtpOps;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
