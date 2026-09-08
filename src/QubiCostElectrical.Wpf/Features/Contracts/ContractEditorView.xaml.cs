using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public partial class ContractEditorView : UserControl
{
    public ContractEditorView() => InitializeComponent();

    public void ApplyNavContext(ContractEditorNavContext ctx)
    {
        if (DataContext is ContractEditorViewModel vm)
        {
            vm.Contract = ctx.Contract ?? new Contract
            {
                Id = ContractId.New(),
                Status = ContractStatus.Borrador
            };
        }
    }
}
