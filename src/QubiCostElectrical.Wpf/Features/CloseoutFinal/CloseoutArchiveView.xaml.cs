using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public partial class CloseoutArchiveView : UserControl
{
    public CloseoutArchiveView(CloseoutArchiveViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
