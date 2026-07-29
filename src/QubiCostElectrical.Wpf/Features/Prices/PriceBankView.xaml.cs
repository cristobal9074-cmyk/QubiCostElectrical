using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Prices;

public partial class PriceBankView : UserControl
{
    public PriceBankView(PriceBankViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public void ApplyNavContext(PriceBankNavigationContext ctx)
    {
        if (DataContext is PriceBankViewModel vm)
            vm.SearchText = ctx.SearchTerm;
    }
}
