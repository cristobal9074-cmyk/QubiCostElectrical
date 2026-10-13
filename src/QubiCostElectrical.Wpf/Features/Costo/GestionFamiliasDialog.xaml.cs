#nullable enable
using System.Windows;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Ventana modal para administración (CRUD) de familias eléctricas del proyecto.
/// </summary>
public partial class GestionFamiliasDialog : Window
{
    private readonly GestionFamiliasViewModel viewModel;

    public GestionFamiliasDialog(GestionFamiliasViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnDialogLoaded;
    }

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        NuevaFamiliaInput.Focus();
    }

    private void OnNuevaFamiliaKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (viewModel.CrearFamiliaCommand.CanExecute(null))
            {
                viewModel.CrearFamiliaCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
