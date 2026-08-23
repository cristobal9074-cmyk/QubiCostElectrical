using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public partial class ContractVersionsView : UserControl
{
    public ContractVersionsView() => InitializeComponent();

    public void ApplyNavContext(ContractVersionsNavContext ctx)
    {
        if (DataContext is ContractVersionsViewModel vm)
        {
            vm.LoadForContract(ctx.ContractId);
        }
    }
}
