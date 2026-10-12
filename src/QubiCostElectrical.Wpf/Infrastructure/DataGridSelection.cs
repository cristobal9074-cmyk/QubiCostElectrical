using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Reglas de selección para las grillas de la app.
///
/// Existe porque la selección <b>por fila</b> y la selección <b>por celda</b> no son intercambiables:
/// las grillas estilo Excel de la app (<c>DataGrid.Workspace.Excel</c> →
/// <c>SelectionUnit=Cell</c>) revientan si alguien marca <c>SelectedItem</c>.
/// </summary>
public static class DataGridSelection
{
    /// <summary>
    /// Marca la fila de <paramref name="item"/> como seleccionada respetando el SelectionUnit de la
    /// grilla.
    ///
    /// En las grillas estilo Excel (<c>SelectionUnit=Cell</c>) asignar <c>SelectedItem</c> es
    /// inválido: al generarse el contenedor de la fila, WPF lanza "El valor actual de la propiedad
    /// SelectionUnit del elemento DataGrid primario impide seleccionar filas" desde
    /// <c>Selector.AdjustNewContainers</c>. Con la fila recién agregada todavía virtualizada, eso deja
    /// el generador de contenedores a medio construir y cada layout posterior vuelve a fallar ("No se
    /// puede llamar a StartAt mientras la generación de contenido está en curso"), congelando la app.
    /// Por eso la vía es celdas + CurrentCell.
    /// </summary>
    /// <returns><c>true</c> si la fila quedó marcada como seleccionada.</returns>
    public static bool SeleccionarFila(DataGrid grid, object? item, DataGridColumn? column)
    {
        if (grid == null || item == null)
            return false;

        if (grid.SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            grid.SelectedItem = item;
            return true;
        }

        var columna = column
            ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible)
            ?? grid.Columns.FirstOrDefault();
        if (columna == null)
            return false;

        grid.SelectedCells.Clear();
        grid.SelectedCells.Add(new DataGridCellInfo(item, columna));
        return true;
    }
}
