using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public partial class ContractEditorView : UserControl
{
    public ContractEditorView(ContractEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
