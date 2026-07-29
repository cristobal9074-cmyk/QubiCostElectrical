using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public partial class CloseoutHandoverView : UserControl
{
    public CloseoutHandoverView(CloseoutHandoverViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
