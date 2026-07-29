using System.Windows;

namespace QubiCostElectrical.Wpf.Features.ActivityEditor;

public partial class DependencyEditorDialog : Window
{
    public DependencyEditorDialog(object viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
