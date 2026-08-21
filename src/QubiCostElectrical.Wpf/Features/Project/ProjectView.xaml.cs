using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Project;

public partial class ProjectView : UserControl
{
    public ProjectView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ProjectViewModel vm)
                vm.Load();
        };
    }
}
