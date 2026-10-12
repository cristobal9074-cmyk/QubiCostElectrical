using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Regresión del cuelgue reportado al agregar material en la lista de precios (Alt+V → Alta rápida).
///
/// Con <c>SelectionUnit=Cell</c> (estilo <c>DataGrid.Workspace.Excel</c>) asignar <c>SelectedItem</c>
/// es inválido: al generarse el contenedor de una fila, WPF lanza "El valor actual de la propiedad
/// SelectionUnit del elemento DataGrid primario impide seleccionar filas" desde
/// <c>Selector.AdjustNewContainers</c>. Como la fila recién agregada todavía está virtualizada, el
/// error deja el generador de contenedores a medio construir y cada layout posterior vuelve a fallar
/// ("No se puede llamar a StartAt mientras la generación de contenido está en curso"): la ventana
/// deja de responder y solo se recupera matando el proceso.
///
/// La invariante que se prueba es la que rompía la app: <c>SelectedItem</c> debe quedar SIEMPRE en
/// null en una grilla de celdas, porque cualquier selección de fila pendiente es la que revienta al
/// realizarse el contenedor.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class DataGridSelectionTests
{
    public DataGridSelectionTests() => WpfTestHost.EnsureInitialized();

    /// <summary>
    /// Aísla el host STA compartido: <c>Application.Current</c> es global y, si queda vivo al
    /// terminar, el resto de la suite engancha trabajo de dispatcher sin pump y las pruebas
    /// siguientes se cuelgan (ver <see cref="XamlViewSmokeHost"/>). Hay que limpiarlo SIEMPRE,
    /// incluso si la prueba falla.
    /// </summary>
    private static void ConHost(Action cuerpo)
    {
        XamlViewSmokeHost.Run(() =>
        {
            try { cuerpo(); }
            finally { XamlViewSmokeHost.ClearApplicationCurrent(); }
        });
    }

    private static DataGrid CrearGrilla(DataGridSelectionUnit unit, params string[] filas)
    {
        var grid = new DataGrid
        {
            SelectionUnit = unit,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            Height = 200,
            Width = 400,
            ItemsSource = new ObservableCollection<string>(filas)
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding() });
        return grid;
    }

    [Fact]
    public void SeleccionarFila_EnGrillaDeCeldas_NoMarcaSelectedItem()
    {
        ConHost(() =>
        {
            var grid = CrearGrilla(DataGridSelectionUnit.Cell, "TAB-001", "TAB-002", "TAB-003");

            bool aplicado = DataGridSelection.SeleccionarFila(grid, grid.Items[2], grid.Columns[0]);

            Assert.True(aplicado);
            // Lo que reventaba la app: dejar una selección de fila pendiente en una grilla de celdas.
            Assert.Null(grid.SelectedItem);
            Assert.Single(grid.SelectedCells);
        });
    }

    [Fact]
    public void SeleccionarFila_EnGrillaPorFila_MarcaSelectedItem()
    {
        ConHost(() =>
        {
            var grid = CrearGrilla(DataGridSelectionUnit.FullRow, "TAB-001", "TAB-002");
            var objetivo = grid.Items[1];

            bool aplicado = DataGridSelection.SeleccionarFila(grid, objetivo, grid.Columns[0]);

            Assert.True(aplicado);
            Assert.Same(objetivo, grid.SelectedItem);
        });
    }

    /// <summary>
    /// El caso exacto del alta rápida: hay una fila seleccionada, se agrega un material y hay que
    /// dejar la fila nueva seleccionada. Si esa selección se hiciera por fila, al realizarse el
    /// contenedor nuevo se dispara la excepción que congela la ventana.
    /// </summary>
    [Fact]
    public void SeleccionarFila_AlPasarAUnaFilaNueva_NoDejaSeleccionDeFilaPendiente()
    {
        ConHost(() =>
        {
            var filas = new ObservableCollection<string> { "TAB-001", "TAB-002" };
            var grid = CrearGrilla(DataGridSelectionUnit.Cell);
            grid.ItemsSource = filas;

            DataGridSelection.SeleccionarFila(grid, filas[0], grid.Columns[0]);
            grid.CurrentCell = new DataGridCellInfo(filas[0], grid.Columns[0]);

            // Alta rápida: se agrega el material y queda seleccionado.
            filas.Add("TAB-003");
            bool aplicado = DataGridSelection.SeleccionarFila(grid, filas[2], grid.Columns[0]);
            grid.CurrentCell = new DataGridCellInfo(filas[2], grid.Columns[0]);

            Assert.True(aplicado);
            Assert.Null(grid.SelectedItem);
            Assert.Single(grid.SelectedCells);
        });
    }

    /// <summary>La grilla real del diálogo arranca en selección por celdas: si eso cambia, la guarda del helper queda sin efecto.</summary>
    [Fact]
    public void EstiloExcel_EsSeleccionDeCeldas()
    {
        ConHost(() =>
        {
            var estilo = (Style)System.Windows.Application.Current!.FindResource("DataGrid.Workspace.Excel");
            var grilla = new DataGrid { Style = estilo };

            Assert.Equal(DataGridSelectionUnit.Cell, grilla.SelectionUnit);
        });
    }

    [Fact]
    public void SeleccionarFila_SinItemOColumna_NoLanza()
    {
        ConHost(() =>
        {
            var sinItem = CrearGrilla(DataGridSelectionUnit.Cell, "TAB-001");
            Assert.False(DataGridSelection.SeleccionarFila(sinItem, null, sinItem.Columns[0]));

            var sinColumnas = new DataGrid { SelectionUnit = DataGridSelectionUnit.Cell };
            Assert.False(DataGridSelection.SeleccionarFila(sinColumnas, "TAB-001", null));
        });
    }
}
