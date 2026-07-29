using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Epp;

public partial class EppView : UserControl
{
    public EppView() => InitializeComponent();

    private void OnCatalogClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EppViewModel vm || !vm.CanEdit)
            return;

        var target = vm.AccOpcionalesSecciones.Count > 0
            ? vm.AccOpcionalesSecciones[0]
            : null;
        vm.PrepareAddFromCatalog(target, "opc");

        var menu = new ContextMenu();
        foreach (var entry in vm.Catalog)
        {
            var item = new MenuItem
            {
                Header = $"{entry.Concepto}  ·  {entry.PrecioLabel}",
                Command = vm.AddCatalogItemCommand,
                CommandParameter = entry
            };
            menu.Items.Add(item);
        }

        menu.PlacementTarget = CatalogButton;
        menu.IsOpen = true;
    }
}
