using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Costo;

public partial class AgregarFamiliaPresentacionDialog : Window
{
    private readonly IEnumerable<string>? _familiasExistentes;

    public string FamiliaNombre { get; private set; } = "";
    public decimal FactorInicial { get; private set; } = 1.45m;

    public AgregarFamiliaPresentacionDialog(IEnumerable<string>? familiasExistentes = null)
    {
        InitializeComponent();
        _familiasExistentes = familiasExistentes;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        NombreBox.Focus();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        var nombre = (NombreBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MostrarError("Por favor ingrese el nombre de la familia.");
            NombreBox.Focus();
            return;
        }

        var clave = FamilyPresentationDefaults.Normalize(nombre);

        if (_familiasExistentes != null && _familiasExistentes.Any(f =>
            string.Equals(FamilyPresentationDefaults.Normalize(f), clave, StringComparison.OrdinalIgnoreCase)))
        {
            MostrarError($"Ya existe una familia con el nombre o sinónimo «{nombre}».");
            NombreBox.Focus();
            return;
        }

        var factorText = (FactorBox.Text ?? "").Trim().Replace(',', '.');
        if (!decimal.TryParse(factorText, NumberStyles.Any, CultureInfo.InvariantCulture, out var factor) || factor < 1.0m || factor > 10.0m)
        {
            MostrarError("El factor de presentación debe ser un número válido entre 1,00 y 10,00.");
            FactorBox.Focus();
            return;
        }

        FamiliaNombre = nombre;
        FactorInicial = factor;
        DialogResult = true;
        Close();
    }

    private void MostrarError(string mensaje)
    {
        MensajeErrorText.Text = mensaje;
        MensajeErrorText.Foreground = (Brush)FindResource("Brush.Danger");
    }
}
