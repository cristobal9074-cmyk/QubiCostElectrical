using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Selector del presupuesto de origen para cargar la configuracion de factores de presentacion.
/// </summary>
public partial class CargarConfiguracionDialog : Window
{
    public CargarConfiguracionDialog(IReadOnlyList<RecentProjectInfo> proyectos)
    {
        InitializeComponent();
        ProyectosList.ItemsSource = proyectos;
        if (proyectos.Count > 0)
            ProyectosList.SelectedIndex = 0;
    }

    /// <summary>True cuando el usuario confirmo la carga.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Ruta del presupuesto elegido.</summary>
    public string? SelectedPath => (ProyectosList.SelectedItem as RecentProjectInfo)?.Path;

    private void OnCargar(object sender, RoutedEventArgs e)
    {
        if (SelectedPath == null)
        {
            HintText.Text = "Selecciona un presupuesto de origen.";
            return;
        }
        Accepted = true;
        DialogResult = true;
    }

    private void OnCancelar(object sender, RoutedEventArgs e)
    {
        Accepted = false;
        DialogResult = false;
    }

    private void OnProyectoDobleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedPath == null)
            return;
        Accepted = true;
        DialogResult = true;
    }
}
