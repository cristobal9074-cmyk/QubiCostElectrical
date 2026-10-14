using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class ConjuntoPickerDialog : Window
{
    private readonly List<ConjuntoElectricoRow> allConjuntos;
    private readonly decimal partidaCantidad;

    public ConjuntoPickerDialog(IEnumerable<ConjuntoElectricoRow> conjuntos, decimal partidaCantidad = 0m)
    {
        allConjuntos = conjuntos.ToList();
        this.partidaCantidad = partidaCantidad;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ConjuntoList.ItemsSource = allConjuntos;
            if (this.partidaCantidad > 0m)
            {
                ChkUsarCantidadPartida.Content = $"Calcular desde la cantidad oficial de la partida ({this.partidaCantidad:G29}) (ideal para alimentadores)";
            }
        };
    }

    public ConjuntoElectricoRow? SelectedConjunto { get; private set; }
    public bool Reemplazar => RbReemplazar.IsChecked == true;
    public bool UsarCantidadPartida => ChkUsarCantidadPartida.IsChecked == true;

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = FilterBox.Text.Trim();
        ConjuntoList.ItemsSource = string.IsNullOrWhiteSpace(text)
            ? allConjuntos
            : allConjuntos.Where(c =>
                c.Nombre.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                c.Codigo.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                c.TipoConjunto.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void ConjuntoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sel = ConjuntoList.SelectedItem as ConjuntoElectricoRow;
        BtnAplicar.IsEnabled = sel != null;
        PreviewGrid.ItemsSource = sel?.Recursos;

        if (sel != null && partidaCantidad > 0m && string.Equals(sel.TipoConjunto, ConjuntoTipo.Alimentador, StringComparison.OrdinalIgnoreCase))
        {
            ChkUsarCantidadPartida.IsChecked = true;
        }

        if (sel != null && sel.Recursos.Count == 0)
        {
            WarnText.Text = "Este conjunto no tiene recursos definidos.";
            WarnText.Visibility = Visibility.Visible;
        }
        else
        {
            WarnText.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnAplicar_Click(object sender, RoutedEventArgs e)
    {
        SelectedConjunto = ConjuntoList.SelectedItem as ConjuntoElectricoRow;
        if (SelectedConjunto == null) return;
        DialogResult = true;
    }
}
