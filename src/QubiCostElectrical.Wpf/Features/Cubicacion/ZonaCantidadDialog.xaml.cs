using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Pregunta en qué zona y con qué cantidad entra en la cubicación el material recién creado en la
/// lista de precios. La cantidad no se escribe en la columna CANT (esa es calculada: suma de las
/// casillas de zona), así que se escribe en la CASILLA de la zona elegida — la misma que el usuario
/// llenaría a mano en la grilla, tal como se pidió: "que se ponga automáticamente la casilla de
/// cubicación". El índice elegido es el mismo offset que usa la columna de esa zona
/// (ver CubicacionSectorService.ZonasActivasOrdenadas).
/// </summary>
public partial class ZonaCantidadDialog : Window
{
    private readonly IReadOnlyList<ZonaProyecto> zonas;

    /// <summary>Casilla (offset 0-based) elegida según el selector. 0 cuando el proyecto no tiene zonas.</summary>
    public int Offset => zonas.Count == 0 ? 0 : Math.Clamp(ZonaCombo.SelectedIndex, 0, zonas.Count - 1);

    /// <summary>
    /// Cantidad escrita en la casilla, normalizada para el motor de fórmulas (invariante). Cadena
    /// vacía mientras lo tecleado no sea una cantidad válida mayor que 0.
    /// </summary>
    public string CantidadTexto => TryLeerCantidad(CantidadBox.Text, out decimal cantidad) && cantidad > 0m
        ? cantidad.ToString("0.##", CultureInfo.InvariantCulture)
        : "";

    public ZonaCantidadDialog(
        IReadOnlyList<ZonaProyecto> zonasActivasOrdenadas,
        string? codigoMaterial,
        string? descripcionMaterial,
        string? partidaDescripcion)
    {
        InitializeComponent();
        zonas = zonasActivasOrdenadas ?? Array.Empty<ZonaProyecto>();

        MaterialText.Text = string.IsNullOrWhiteSpace(codigoMaterial)
            ? (descripcionMaterial ?? "")
            : $"{codigoMaterial} · {descripcionMaterial}";
        PartidaText.Text = string.IsNullOrWhiteSpace(partidaDescripcion)
            ? "Sin partida seleccionada"
            : $"Partida: {partidaDescripcion}";

        if (zonas.Count == 0)
        {
            // Sin zonas configuradas el encabezado de la primera casilla está vacío: se avisa y se
            // usa la casilla 1 en vez de mostrar un selector con opciones fantasma.
            ZonaCombo.Visibility = Visibility.Collapsed;
            SinZonasBanner.Visibility = Visibility.Visible;
            Loaded += (_, _) => { CantidadBox.Focus(); CantidadBox.SelectAll(); };
            return;
        }

        ZonaCombo.ItemsSource = zonas
            .Select((z, i) => string.IsNullOrWhiteSpace(z.Nombre) ? $"Zona {i + 1}" : z.Nombre.Trim())
            .ToList();
        ZonaCombo.SelectedIndex = 0;
        // Con varias zonas el foco arranca en el selector (se pregunta la zona); con una sola no hay
        // nada que elegir, así que arranca en la cantidad y el flujo se resuelve con Enter.
        Loaded += (_, _) =>
        {
            if (zonas.Count > 1)
            {
                ZonaCombo.Focus();
                return;
            }
            CantidadBox.Focus();
            CantidadBox.SelectAll();
        };
    }

    /// <summary>Enter en el selector de zona no abre la lista: baja a la cantidad, que es lo que falta.</summary>
    private void OnZonaComboKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
            return;

        e.Handled = true;
        CantidadBox.Focus();
        CantidadBox.SelectAll();
    }

    private void OnCubicarClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CantidadTexto))
        {
            WpfDialogService.Info("Cantidad inválida", "Escriba una cantidad mayor que 0 (por ejemplo 1).");
            CantidadBox.Focus();
            CantidadBox.SelectAll();
            return;
        }

        DialogResult = true;
    }

    /// <summary>
    /// Acepta "1", "1,5" y "1.5" con la MISMA regla que el motor de fórmulas de las casillas
    /// (QuantityFormulaService: "," → "." y parse invariante), para que lo que se escribe acá valga
    /// exactamente lo mismo que si el usuario tecleara la casilla a mano. Parsear con la cultura
    /// local leería "1.5" como 15 (el punto es separador de miles en es-CL).
    /// </summary>
    internal static bool TryLeerCantidad(string? texto, out decimal cantidad)
    {
        cantidad = 0m;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        string limpio = texto.Trim().Replace(" ", "").Replace(',', '.');
        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out cantidad);
    }
}
