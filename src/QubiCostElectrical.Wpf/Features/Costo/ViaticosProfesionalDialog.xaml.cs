using System.Windows;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Costo;

public partial class ViaticosProfesionalDialog : Window
{
    public ViaticosProfesionalViewModel ViewModel { get; }

    public ViaticosProfesionalDialog(ViaticosProfesionalViewModel vm)
    {
        ViewModel = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
