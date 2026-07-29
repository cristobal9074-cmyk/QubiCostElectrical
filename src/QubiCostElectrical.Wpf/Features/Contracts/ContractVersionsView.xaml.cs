using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public partial class ContractVersionsView : UserControl
{
    public ContractVersionsView(ContractVersionsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
