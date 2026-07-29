using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public partial class CloseoutReceptionView : UserControl
{
    public CloseoutReceptionView(CloseoutReceptionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
