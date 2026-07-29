using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public partial class CloseoutLessonsView : UserControl
{
    public CloseoutLessonsView(CloseoutLessonsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
