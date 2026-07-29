using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public partial class SolicitudesView : UserControl
{
    public SolicitudesView()
    {
        InitializeComponent();
    }

    private void OnPrintClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SolicitudesViewModel vm)
        {
            vm.ExportPdfCommand.Execute(null);
        }
    }
}
