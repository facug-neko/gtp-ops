using System.Windows;
using System.Windows.Controls;
using AxiomOps.Services.Models;
using AxiomOps.UI.ViewModels;

namespace AxiomOps.UI.Views;

public partial class FilesView : UserControl
{
    public FilesView()
    {
        InitializeComponent();
    }

    // TreeView.SelectedItem is read-only, so the selection flows to the VM here.
    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is FilesViewModel viewModel)
        {
            viewModel.OnTreeSelectionChanged(e.NewValue as FileFolderNode);
        }
    }
}
