using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Hoja de Cubicación compartida: herramienta de apoyo manual (no persiste en el proyecto) donde
/// el usuario puede agregar ítems (partidas) y, dentro de cada uno, filas de datos con suma
/// automática — útil para sumar mediciones sueltas antes de trasladarlas a la cubicación real.
/// </summary>
public partial class HojaCubicacionDialog : Window
{
    // Como una hoja de Excel real: cada ítem nuevo parte con 300 columnas de datos disponibles
    // (C en adelante), no solo las 2-3 que se necesitan al principio.
    private const int DefaultColumnCount = 300;

    private readonly ObservableCollection<HojaCubicacionItemVm> items = new();
    private readonly Dictionary<HojaCubicacionItemVm, DataGrid> itemGrids = new();
    // Evita reconstruir las columnas si Loaded llega a dispararse más de una vez para la misma
    // grilla — reconstruir columnas mientras la fila 0 ya tiene celdas generadas contra las
    // columnas viejas la dejaba "pegada" en un estado inconsistente (no editable), aunque las
    // filas agregadas después quedaran bien con las columnas nuevas.
    private readonly HashSet<DataGrid> loadedGrids = new();

    /// <summary>Ítems actuales — expuesto para que el llamador los guarde en memoria y los pase
    /// de vuelta la próxima vez que se abra el diálogo, así el contenido sobrevive a cerrar la
    /// ventana (Esc, botón Cerrar o la X) dentro de la misma sesión del proyecto.</summary>
    public IReadOnlyList<HojaCubicacionItemVm> Items => items;

    /// <summary>En modo selección, las celdas de la columna Suma (B) se vuelven "clickeables":
    /// un clic ahí cierra el diálogo y entrega ese valor a quien lo abrió (ver <see cref="PickValue"/>).
    /// El resto de la hoja sigue totalmente editable mientras tanto.</summary>
    public bool IsPickMode { get; private init; }

    /// <summary>Identificador del destino que solicita el valor (ej. ID de fila de presupuesto).
    /// Permite reemplazar y desmarcar automáticamente cualquier selección previa hecha para este mismo destino.</summary>
    public string? TargetKey { get; set; }

    /// <summary>Valor de la celda Suma que el usuario eligió en modo selección, o null si cerró
    /// sin elegir nada (Esc / botón Cerrar / X).</summary>
    public decimal? PickedValue { get; private set; }

    /// <summary>Abre la Hoja de Cubicación en modo selección: el usuario puede seguir editando
    /// libremente, pero un clic sobre cualquier celda de la columna Suma (B) cierra el diálogo y
    /// devuelve ese valor — pensado para traerlo directo a una celda de Cubicación (ej. Cantidad).
    /// Devuelve también los ítems actuales, para que el llamador los guarde igual que en el uso
    /// normal (cualquier edición hecha mientras se elegía no debe perderse).</summary>
}
