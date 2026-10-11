using System.Windows;

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



    private void OnMoreActionsClick(object sender, RoutedEventArgs e)

    {

        if (sender is Button { ContextMenu: { } menu } button)

        {

            menu.PlacementTarget = button;

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

            menu.DataContext = DataContext;

            menu.IsOpen = true;

        }

    }

}

